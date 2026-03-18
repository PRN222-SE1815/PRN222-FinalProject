using System.Net;
using System.Text;
using System.Text.Json;
using BusinessLogic.DTOs.Response;
using BusinessLogic.DTOs.Responses.AI;
using BusinessLogic.Services.Interfaces;
using BusinessLogic.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessLogic.Services.Implements;

public sealed class GeminiClientService : IGeminiClientService
{
    private const int MaxRetryAttempts = 2;
    private const string GroundingInstruction =
        "\n\n[GROUNDING] You MUST only draw conclusions from tool results provided in the conversation. " +
        "If you lack sufficient data, clearly state what information is missing. Never fabricate or assume data.";

    private const string StructuredOutputInstruction =
        "\n\n[OUTPUT FORMAT] You MUST respond with ONLY a valid JSON object (no markdown fences, no extra text). " +
        "The JSON schema:\n" +
        "{\n" +
        "  \"purpose\": \"SCORE_SUMMARY|STUDY_PLAN|COURSE_SUGGESTION\",\n" +
        "  \"summaryCards\": [{\"key\":\"...\",\"label\":\"...\",\"value\":\"...\",\"unit\":\"...\"}],\n" +
        "  \"riskFlags\": [{\"courseCode\":\"...\",\"message\":\"...\",\"severity\":\"LOW|MEDIUM|HIGH\"}],\n" +
        "  \"recommendedActions\": [{\"title\":\"...\",\"detail\":\"...\",\"priority\":1}],\n" +
        "  \"plans\": [{\"planName\":\"...\",\"totalCredits\":0,\"constraintsOk\":true,\"courseCodes\":[],\"notes\":[]}],\n" +
        "  \"disclaimer\": \"...\"\n" +
        "}\n" +
        "All arrays may be empty if not applicable. priority is 1..5. severity is LOW|MEDIUM|HIGH. " +
        "Do NOT wrap in markdown code fences. Return ONLY the raw JSON object.";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GeminiSettings _settings;
    private readonly ILogger<GeminiClientService> _logger;

    public GeminiClientService(
        IHttpClientFactory httpClientFactory,
        IOptions<GeminiSettings> settings,
        ILogger<GeminiClientService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<string>> GenerateAsync(
        string model,
        string systemPrompt,
        IReadOnlyList<AIChatMessageResponse> messages,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(systemPrompt))
        {
            return ServiceResult<string>.Fail("INVALID_INPUT", "Model and system prompt are required.");
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogError("Gemini API key is not configured — check User Secrets or environment variables");
            return ServiceResult<string>.Fail("AI_PROVIDER_ERROR", "Gemini API key is not configured.");
        }

        try
        {
            var groundedPrompt = systemPrompt + GroundingInstruction + StructuredOutputInstruction;
            var payload = BuildGeneratePayload(groundedPrompt, messages);
            return await SendGenerateRequestAsync(model, payload, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini GenerateAsync failed for model {Model}", model);
            return ServiceResult<string>.Fail("INTERNAL_ERROR", "Gemini request failed due to a system error.");
        }
    }

    public async Task<ServiceResult<GeminiToolDecisionDto>> GenerateWithToolsAsync(
        string model,
        string systemPrompt,
        IReadOnlyList<AIChatMessageResponse> messages,
        IReadOnlyList<string> toolSchemasJson,
        CancellationToken ct = default)
    {
        var toolInstruction = BuildToolInstruction(toolSchemasJson);
        var prompt = $"{systemPrompt}\n\n{toolInstruction}";

        var textResult = await GenerateAsync(model, prompt, messages, ct);
        if (!textResult.IsSuccess || textResult.Data is null)
        {
            return ServiceResult<GeminiToolDecisionDto>.Fail(
                textResult.ErrorCode ?? "AI_PROVIDER_ERROR",
                textResult.Message);
        }

        var parsedDecision = TryParseToolDecision(textResult.Data, toolSchemasJson);
        return ServiceResult<GeminiToolDecisionDto>.Success(parsedDecision);
    }

    private static string BuildToolInstruction(IReadOnlyList<string> toolSchemasJson)
    {
        var tools = toolSchemasJson.Count == 0
            ? "[]"
            : $"[{string.Join(',', toolSchemasJson)}]";

        return $"Available tools (JSON schema list): {tools}. " +
               "If a tool is needed, respond strictly as JSON: " +
               "{\"requiresToolCall\":true,\"toolName\":\"...\",\"toolArgumentsJson\":\"{...}\",\"assistantText\":\"...\"}. " +
               "If no tool is needed, respond as JSON: {\"requiresToolCall\":false,\"assistantText\":\"...\"}. " +
               "You may ONLY call tools from the available list above. Do NOT invent tool names.";
    }

    private object BuildGeneratePayload(string systemPrompt, IReadOnlyList<AIChatMessageResponse> messages)
    {
        var parts = new List<object>
        {
            new { text = systemPrompt }
        };

        foreach (var message in messages)
        {
            if (string.IsNullOrWhiteSpace(message.Content))
            {
                continue;
            }

            var rolePrefix = message.SenderType?.ToUpperInvariant() switch
            {
                "USER" => "User",
                "SYSTEM" => "System",
                _ => "Assistant"
            };
            parts.Add(new { text = $"{rolePrefix}: {message.Content}" });
        }

        return new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts
                }
            },
            generationConfig = new
            {
                maxOutputTokens = _settings.MaxOutputTokens
            }
        };
    }

    private async Task<ServiceResult<string>> SendGenerateRequestAsync(string model, object payload, CancellationToken ct)
    {
        var baseUrl = (_settings.BaseUrl ?? string.Empty).TrimEnd('/');
        var requestUrl = $"{baseUrl}/v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(_settings.ApiKey)}";

        for (var attempt = 0; attempt <= MaxRetryAttempts; attempt++)
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            linkedCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _settings.TimeoutSeconds)));

            try
            {
                var client = _httpClientFactory.CreateClient();
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUrl)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);

                if (IsTransient(response.StatusCode) && attempt < MaxRetryAttempts)
                {
                    _logger.LogWarning("Gemini transient {StatusCode} on attempt {Attempt}, retrying",
                        (int)response.StatusCode, attempt + 1);
                    await Task.Delay(GetBackoffDelay(attempt), ct);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Gemini provider returned non-success status {StatusCode} on attempt {Attempt}",
                        (int)response.StatusCode, attempt + 1);
                    return ServiceResult<string>.Fail("AI_PROVIDER_ERROR", "AI provider returned an error response.");
                }

                var responseContent = await response.Content.ReadAsStringAsync(ct);
                var text = ExtractGeminiText(responseContent);
                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogWarning("Gemini returned empty candidate text");
                    return ServiceResult<string>.Fail("AI_PROVIDER_ERROR", "AI provider returned empty content.");
                }

                return ServiceResult<string>.Success(text);
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested && attempt < MaxRetryAttempts)
            {
                _logger.LogWarning(ex, "Gemini request timeout on attempt {Attempt}", attempt + 1);
                await Task.Delay(GetBackoffDelay(attempt), ct);
            }
            catch (HttpRequestException ex) when (attempt < MaxRetryAttempts)
            {
                _logger.LogWarning(ex, "Gemini transient HTTP error on attempt {Attempt}", attempt + 1);
                await Task.Delay(GetBackoffDelay(attempt), ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini request failed on attempt {Attempt}", attempt + 1);
                return ServiceResult<string>.Fail("INTERNAL_ERROR", "Unexpected error while calling AI provider.");
            }
        }

        return ServiceResult<string>.Fail("AI_PROVIDER_ERROR", "AI provider request failed after retries.");
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        var code = (int)statusCode;
        return code is >= 500 and <= 599 or 429;
    }

    private static TimeSpan GetBackoffDelay(int attempt)
    {
        return TimeSpan.FromMilliseconds(300 * Math.Pow(2, attempt));
    }

    private static string? ExtractGeminiText(string responseJson)
    {
        try
        {
            using var jsonDocument = JsonDocument.Parse(responseJson);
            if (!jsonDocument.RootElement.TryGetProperty("candidates", out var candidates)
                || candidates.ValueKind != JsonValueKind.Array
                || candidates.GetArrayLength() == 0)
            {
                return null;
            }

            var candidate = candidates[0];
            if (!candidate.TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.ValueKind != JsonValueKind.Array
                || parts.GetArrayLength() == 0)
            {
                return null;
            }

            var firstPart = parts[0];
            return firstPart.TryGetProperty("text", out var textElement)
                ? textElement.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private GeminiToolDecisionDto TryParseToolDecision(string responseText, IReadOnlyList<string> toolSchemasJson)
    {
        try
        {
            var normalized = responseText.Trim();

            // Strip markdown code fences if present
            if (normalized.StartsWith("```"))
            {
                var firstNewline = normalized.IndexOf('\n');
                if (firstNewline > 0) normalized = normalized[(firstNewline + 1)..];
                if (normalized.EndsWith("```")) normalized = normalized[..^3];
                normalized = normalized.Trim();
            }

            using var document = JsonDocument.Parse(normalized);
            var root = document.RootElement;

            var requiresToolCall = root.TryGetProperty("requiresToolCall", out var requiresToolCallElement)
                && requiresToolCallElement.ValueKind == JsonValueKind.True;

            var decision = new GeminiToolDecisionDto
            {
                RequiresToolCall = requiresToolCall,
                ToolName = root.TryGetProperty("toolName", out var toolNameElement) ? toolNameElement.GetString() : null,
                ToolArgumentsJson = root.TryGetProperty("toolArgumentsJson", out var argumentsElement)
                    ? (argumentsElement.ValueKind == JsonValueKind.String
                        ? argumentsElement.GetString()
                        : argumentsElement.GetRawText())
                    : null,
                AssistantText = root.TryGetProperty("assistantText", out var assistantTextElement) ? assistantTextElement.GetString() : null
            };

            // Reject tool calls for tools not in the provided schema list
            if (decision.RequiresToolCall && !string.IsNullOrWhiteSpace(decision.ToolName))
            {
                var isKnownTool = false;
                foreach (var schema in toolSchemasJson)
                {
                    if (schema.Contains($"\"{decision.ToolName}\"", StringComparison.Ordinal))
                    {
                        isKnownTool = true;
                        break;
                    }
                }

                if (!isKnownTool)
                {
                    _logger.LogWarning("Gemini requested unknown tool {ToolName}, rejecting tool call", decision.ToolName);
                    decision.RequiresToolCall = false;
                    decision.AssistantText ??= responseText;
                }
            }

            if (!decision.RequiresToolCall && string.IsNullOrWhiteSpace(decision.AssistantText))
            {
                decision.AssistantText = responseText;
            }

            return decision;
        }
        catch
        {
            return new GeminiToolDecisionDto
            {
                RequiresToolCall = false,
                AssistantText = responseText
            };
        }
    }
}
