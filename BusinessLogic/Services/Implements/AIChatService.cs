using System.Collections.Concurrent;
using System.Text.Json;
using BusinessLogic.DTOs.Requests.AI;
using BusinessLogic.DTOs.Response;
using BusinessLogic.DTOs.Responses.AI;
using BusinessLogic.Services.Interfaces;
using BusinessLogic.Settings;
using BusinessObject.Entities;
using BusinessObject.Enum;
using DataAccess.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessLogic.Services.Implements;

public sealed class AIChatService : IAIChatService
{
    private static readonly HashSet<string> AllowedPurposes =
    [
        "SCORE_SUMMARY",
        "STUDY_PLAN",
        "COURSE_SUGGESTION"
    ];

    private static readonly HashSet<string> ValidSeverities = ["LOW", "MEDIUM", "HIGH"];

    private static readonly JsonSerializerOptions JsonCaseInsensitive = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly ConcurrentDictionary<int, SlidingWindowCounter> RateLimitCounters = new();

    private readonly IAIChatRepository _aiChatRepository;
    private readonly IAIToolService _aiToolService;
    private readonly IGeminiClientService _geminiClientService;
    private readonly IUserRepository _userRepository;
    private readonly GeminiSettings _geminiSettings;
    private readonly AIChatSettings _chatSettings;
    private readonly ILogger<AIChatService> _logger;

    public AIChatService(
        IAIChatRepository aiChatRepository,
        IAIToolService aiToolService,
        IGeminiClientService geminiClientService,
        IUserRepository userRepository,
        IOptions<GeminiSettings> geminiOptions,
        IOptions<AIChatSettings>? chatOptions,
        ILogger<AIChatService> logger)
    {
        _aiChatRepository = aiChatRepository;
        _aiToolService = aiToolService;
        _geminiClientService = geminiClientService;
        _userRepository = userRepository;
        _geminiSettings = geminiOptions.Value;
        _chatSettings = chatOptions?.Value ?? new AIChatSettings();
        _logger = logger;
    }

    public async Task<ServiceResult<AIChatSessionResponse>> StartSessionAsync(
        int userId,
        string actorRole,
        StartChatSessionRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (!await IsStudentActorAsync(userId, actorRole))
            {
                return ServiceResult<AIChatSessionResponse>.Fail("FORBIDDEN", "Only STUDENT role is allowed.");
            }

            if (request is null || string.IsNullOrWhiteSpace(request.Purpose))
            {
                return ServiceResult<AIChatSessionResponse>.Fail("INVALID_INPUT", "Purpose is required.");
            }

            var normalizedPurpose = request.Purpose.Trim().ToUpperInvariant();
            if (!AllowedPurposes.Contains(normalizedPurpose))
            {
                return ServiceResult<AIChatSessionResponse>.Fail("INVALID_INPUT", "Unsupported chat session purpose.");
            }

            var session = new AIChatSession
            {
                UserId = userId,
                Purpose = normalizedPurpose,
                ModelName = _geminiSettings.Model,
                State = AIChatSessionState.ACTIVE.ToString(),
                PromptVersion = string.IsNullOrWhiteSpace(request.PromptVersion) ? "v1" : request.PromptVersion,
                CreatedAt = DateTime.UtcNow
            };

            var created = await _aiChatRepository.CreateSessionAsync(session, ct);
            _logger.LogInformation("AI session started — SessionId={SessionId}, UserId={UserId}, Purpose={Purpose}",
                created.ChatSessionId, userId, normalizedPurpose);

            return ServiceResult<AIChatSessionResponse>.Success(MapSession(created));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StartSessionAsync failed for user {UserId}", userId);
            return ServiceResult<AIChatSessionResponse>.Fail("INTERNAL_ERROR", "Unexpected system error.");
        }
    }

    public async Task<ServiceResult<AIChatTurnResponse>> SendMessageAsync(
        int userId,
        string actorRole,
        SendChatMessageRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (!await IsStudentActorAsync(userId, actorRole))
            {
                return ServiceResult<AIChatTurnResponse>.Fail("FORBIDDEN", "Only STUDENT role is allowed.");
            }

            if (request is null || request.ChatSessionId <= 0 || string.IsNullOrWhiteSpace(request.Message))
            {
                return ServiceResult<AIChatTurnResponse>.Fail("INVALID_INPUT", "Invalid chat message request.");
            }

            if (request.Message.Length > _chatSettings.MaxUserMessageLength)
            {
                return ServiceResult<AIChatTurnResponse>.Fail("INVALID_INPUT",
                    $"Message exceeds max length of {_chatSettings.MaxUserMessageLength} characters.");
            }

            // Rate limiting
            if (!TryConsumeRateLimit(userId))
            {
                _logger.LogWarning("Rate limit exceeded for user {UserId}", userId);
                return ServiceResult<AIChatTurnResponse>.Fail("RATE_LIMITED",
                    "You are sending messages too quickly. Please wait a moment.");
            }

            var session = await _aiChatRepository.GetSessionByIdAsync(request.ChatSessionId, ct);
            if (session is null)
            {
                return ServiceResult<AIChatTurnResponse>.Fail("SESSION_NOT_FOUND", "Chat session not found.");
            }

            if (session.UserId != userId)
            {
                _logger.LogWarning("Ownership violation — UserId={UserId} tried to access SessionId={SessionId} owned by {OwnerId}",
                    userId, session.ChatSessionId, session.UserId);
                return ServiceResult<AIChatTurnResponse>.Fail("FORBIDDEN", "You do not own this chat session.");
            }

            if (!string.Equals(session.State, AIChatSessionState.ACTIVE.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<AIChatTurnResponse>.Fail("INVALID_STATE", "Chat session is not active.");
            }

            // Save user message
            var createdUserMessage = await _aiChatRepository.AddMessageAsync(new AIChatMessage
            {
                ChatSessionId = session.ChatSessionId,
                SenderType = AIChatSenderType.USER.ToString(),
                Content = request.Message.Trim(),
                CreatedAt = DateTime.UtcNow
            }, ct);

            // Load history
            var historyResult = await _aiChatRepository.GetMessagesBySessionAsync(
                session.ChatSessionId, 1, _chatSettings.MaxHistoryMessages, ct);

            var historyMessages = historyResult.Items
                .OrderBy(m => m.CreatedAt)
                .ThenBy(m => m.ChatMessageId)
                .TakeLast(_chatSettings.MaxHistoryMessages)
                .Select(MapMessage)
                .ToList();

            var systemPrompt = BuildSystemPrompt(session.Purpose);
            var modelName = string.IsNullOrWhiteSpace(session.ModelName) ? _geminiSettings.Model : session.ModelName;

            var toolCallResponses = new List<AIToolCallResponse>();
            string? warning = null;
            string assistantText;

            if (request.UseTools)
            {
                var toolSchemas = _aiToolService.GetSupportedToolNames()
                    .Select(name => JsonSerializer.Serialize(new { name }))
                    .ToList();

                var workingHistory = historyMessages.ToList();
                var toolCallCount = 0;
                assistantText = string.Empty;

                // Multi-turn tool loop with cap
                while (toolCallCount < _chatSettings.MaxToolCallsPerTurn)
                {
                    var decisionResult = await _geminiClientService.GenerateWithToolsAsync(
                        modelName, systemPrompt, workingHistory, toolSchemas, ct);

                    if (!decisionResult.IsSuccess || decisionResult.Data is null)
                    {
                        await SaveSystemMessageAsync(session.ChatSessionId,
                            "AI provider error during tool-assisted generation.", ct);
                        return ServiceResult<AIChatTurnResponse>.Fail(
                            decisionResult.ErrorCode ?? "AI_PROVIDER_ERROR", decisionResult.Message);
                    }

                    assistantText = decisionResult.Data.AssistantText ?? string.Empty;

                    if (!decisionResult.Data.RequiresToolCall || string.IsNullOrWhiteSpace(decisionResult.Data.ToolName))
                    {
                        break;
                    }

                    // Validate tool name against supported list
                    var requestedTool = decisionResult.Data.ToolName;
                    if (!_aiToolService.GetSupportedToolNames().Contains(requestedTool, StringComparer.Ordinal))
                    {
                        _logger.LogWarning("Model requested unsupported tool {ToolName}, rejecting", requestedTool);
                        warning = $"AI requested unsupported tool '{requestedTool}', skipped.";

                        await _aiChatRepository.AddToolCallAsync(new AIToolCall
                        {
                            ChatSessionId = session.ChatSessionId,
                            ToolName = requestedTool,
                            RequestJson = decisionResult.Data.ToolArgumentsJson ?? "{}",
                            ResponseJson = "{\"error\":\"INVALID_TOOL\"}",
                            Status = AIToolCallStatus.ERROR.ToString(),
                            CreatedAt = DateTime.UtcNow
                        }, ct);

                        break;
                    }

                    var toolArgumentsJson = string.IsNullOrWhiteSpace(decisionResult.Data.ToolArgumentsJson)
                        ? "{}" : decisionResult.Data.ToolArgumentsJson;

                    var toolExecutionResult = await _aiToolService.ExecuteToolAsync(
                        userId, actorRole, requestedTool, toolArgumentsJson, ct);

                    var toolCallEntity = await _aiChatRepository.AddToolCallAsync(new AIToolCall
                    {
                        ChatSessionId = session.ChatSessionId,
                        ToolName = requestedTool,
                        RequestJson = toolArgumentsJson,
                        ResponseJson = toolExecutionResult.IsSuccess
                            ? toolExecutionResult.Data
                            : JsonSerializer.Serialize(new { error = toolExecutionResult.ErrorCode, message = toolExecutionResult.Message }),
                        Status = toolExecutionResult.IsSuccess
                            ? AIToolCallStatus.OK.ToString()
                            : AIToolCallStatus.ERROR.ToString(),
                        CreatedAt = DateTime.UtcNow
                    }, ct);

                    toolCallResponses.Add(MapToolCall(toolCallEntity));
                    toolCallCount++;

                    if (!toolExecutionResult.IsSuccess || string.IsNullOrWhiteSpace(toolExecutionResult.Data))
                    {
                        warning = $"Tool '{requestedTool}' execution failed: {toolExecutionResult.Message}";
                        _logger.LogWarning("Tool execution failed — Tool={ToolName}, Error={ErrorCode}",
                            requestedTool, toolExecutionResult.ErrorCode);
                        break;
                    }

                    // Inject tool result into history for next iteration
                    workingHistory.Add(new AIChatMessageResponse
                    {
                        ChatMessageId = 0,
                        ChatSessionId = session.ChatSessionId,
                        SenderType = AIChatSenderType.SYSTEM.ToString(),
                        Content = $"Tool '{requestedTool}' result: {toolExecutionResult.Data}",
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }

                // Guard: tool call cap exceeded
                if (toolCallCount >= _chatSettings.MaxToolCallsPerTurn && string.IsNullOrWhiteSpace(assistantText))
                {
                    _logger.LogWarning("Tool call cap reached — SessionId={SessionId}, ToolCalls={Count}",
                        session.ChatSessionId, toolCallCount);
                    await SaveSystemMessageAsync(session.ChatSessionId,
                        "Maximum tool calls per turn reached.", ct);
                    return ServiceResult<AIChatTurnResponse>.Fail("SAFE_GUARD_TRIGGERED",
                        "Maximum tool calls per turn reached. Please simplify your question.");
                }

                // Final generation with tool context if we have tool results but no final text
                if (toolCallResponses.Count > 0 && string.IsNullOrWhiteSpace(assistantText))
                {
                    var finalGeneration = await _geminiClientService.GenerateAsync(
                        modelName, systemPrompt, workingHistory, ct);

                    if (!finalGeneration.IsSuccess || string.IsNullOrWhiteSpace(finalGeneration.Data))
                    {
                        await SaveSystemMessageAsync(session.ChatSessionId,
                            "AI provider error during final generation.", ct);
                        return ServiceResult<AIChatTurnResponse>.Fail(
                            finalGeneration.ErrorCode ?? "AI_PROVIDER_ERROR", finalGeneration.Message);
                    }

                    assistantText = finalGeneration.Data;
                }
            }
            else
            {
                var generationResult = await _geminiClientService.GenerateAsync(
                    modelName, systemPrompt, historyMessages, ct);

                if (!generationResult.IsSuccess || string.IsNullOrWhiteSpace(generationResult.Data))
                {
                    await SaveSystemMessageAsync(session.ChatSessionId,
                        "AI provider error during generation.", ct);
                    return ServiceResult<AIChatTurnResponse>.Fail(
                        generationResult.ErrorCode ?? "AI_PROVIDER_ERROR", generationResult.Message);
                }

                assistantText = generationResult.Data;
            }

            if (string.IsNullOrWhiteSpace(assistantText))
            {
                assistantText = "I was unable to generate a response. Please try rephrasing your question.";
                warning ??= "AI returned empty content.";
            }

            // Try parse structured JSON schema
            var structuredData = TryParseStructuredResponse(assistantText);
            if (structuredData is null && !string.IsNullOrWhiteSpace(assistantText))
            {
                // Self-repair: ask model to reformat as JSON
                _logger.LogInformation("Structured parse failed, attempting self-repair for session {SessionId}",
                    session.ChatSessionId);

                var repairHistory = historyMessages.ToList();
                repairHistory.Add(new AIChatMessageResponse
                {
                    ChatMessageId = 0,
                    ChatSessionId = session.ChatSessionId,
                    SenderType = AIChatSenderType.ASSISTANT.ToString(),
                    Content = assistantText,
                    CreatedAtUtc = DateTime.UtcNow
                });
                repairHistory.Add(new AIChatMessageResponse
                {
                    ChatMessageId = 0,
                    ChatSessionId = session.ChatSessionId,
                    SenderType = AIChatSenderType.USER.ToString(),
                    Content = "Your previous response was not valid JSON. Please reformat your entire answer strictly as a single raw JSON object matching the required schema. No markdown fences, no extra text.",
                    CreatedAtUtc = DateTime.UtcNow
                });

                var repairResult = await _geminiClientService.GenerateAsync(
                    modelName, systemPrompt, repairHistory, ct);

                if (repairResult.IsSuccess && !string.IsNullOrWhiteSpace(repairResult.Data))
                {
                    var repairedData = TryParseStructuredResponse(repairResult.Data);
                    if (repairedData is not null)
                    {
                        structuredData = repairedData;
                        assistantText = repairResult.Data;
                    }
                    else
                    {
                        _logger.LogWarning("Self-repair also failed structured parse for session {SessionId}",
                            session.ChatSessionId);
                    }
                }
            }

            // Validate schema if parsed
            if (structuredData is not null)
            {
                var validationErrors = ValidateSchema(structuredData);
                if (validationErrors.Count > 0)
                {
                    _logger.LogWarning("Structured schema validation failed for session {SessionId}: {Errors}",
                        session.ChatSessionId, string.Join("; ", validationErrors));
                    warning = "AI response had minor format issues; some data may be incomplete.";
                    // Still use the data — UI will handle missing fields gracefully
                }
            }

            // Save assistant message
            var assistantMessage = await _aiChatRepository.AddMessageAsync(new AIChatMessage
            {
                ChatSessionId = session.ChatSessionId,
                SenderType = AIChatSenderType.ASSISTANT.ToString(),
                Content = assistantText,
                CreatedAt = DateTime.UtcNow
            }, ct);

            _logger.LogInformation("AI turn completed — SessionId={SessionId}, UserId={UserId}, ToolCalls={ToolCallCount}, Structured={HasStructured}",
                session.ChatSessionId, userId, toolCallResponses.Count, structuredData is not null);

            var response = new AIChatTurnResponse
            {
                Session = MapSession(session),
                UserMessage = MapMessage(createdUserMessage),
                AssistantMessage = MapMessage(assistantMessage),
                ToolCalls = toolCallResponses,
                Warning = warning,
                StructuredData = structuredData
            };

            return ServiceResult<AIChatTurnResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendMessageAsync failed for user {UserId}, session {ChatSessionId}",
                userId, request?.ChatSessionId);
            return ServiceResult<AIChatTurnResponse>.Fail("INTERNAL_ERROR", "Unexpected system error.");
        }
    }

    public async Task<ServiceResult<PagedResultDto>> GetSessionHistoryAsync(
        int userId,
        string actorRole,
        long chatSessionId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            if (!await IsStudentActorAsync(userId, actorRole))
            {
                return ServiceResult<PagedResultDto>.Fail("FORBIDDEN", "Only STUDENT role is allowed.");
            }

            if (chatSessionId <= 0)
            {
                return ServiceResult<PagedResultDto>.Fail("INVALID_INPUT", "Invalid chat session id.");
            }

            var session = await _aiChatRepository.GetSessionByIdAsync(chatSessionId, ct);
            if (session is null)
            {
                return ServiceResult<PagedResultDto>.Fail("SESSION_NOT_FOUND", "Chat session not found.");
            }

            if (session.UserId != userId)
            {
                _logger.LogWarning("Ownership violation on history — UserId={UserId}, SessionId={SessionId}",
                    userId, chatSessionId);
                return ServiceResult<PagedResultDto>.Fail("FORBIDDEN", "You do not own this chat session.");
            }

            var messageResult = await _aiChatRepository.GetMessagesBySessionAsync(chatSessionId, page, pageSize, ct);
            var items = messageResult.Items
                .Select(MapMessage)
                .Cast<object>()
                .ToList();

            var result = new PagedResultDto
            {
                Page = page <= 0 ? 1 : page,
                PageSize = pageSize <= 0 ? 20 : pageSize,
                TotalCount = messageResult.TotalCount,
                Items = items
            };

            return ServiceResult<PagedResultDto>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetSessionHistoryAsync failed for user {UserId}, session {ChatSessionId}",
                userId, chatSessionId);
            return ServiceResult<PagedResultDto>.Fail("INTERNAL_ERROR", "Unexpected system error.");
        }
    }

    #region Rate Limiting

    private bool TryConsumeRateLimit(int userId)
    {
        var maxPerMinute = Math.Max(1, _chatSettings.MaxMessagesPerMinutePerUser);
        var counter = RateLimitCounters.GetOrAdd(userId, _ => new SlidingWindowCounter(maxPerMinute));
        counter.UpdateLimit(maxPerMinute);
        return counter.TryConsume();
    }

    private sealed class SlidingWindowCounter
    {
        private readonly object _lock = new();
        private readonly Queue<DateTime> _timestamps = new();
        private int _maxPerMinute;

        public SlidingWindowCounter(int maxPerMinute)
        {
            _maxPerMinute = maxPerMinute;
        }

        public void UpdateLimit(int maxPerMinute)
        {
            _maxPerMinute = maxPerMinute;
        }

        public bool TryConsume()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                var windowStart = now.AddMinutes(-1);

                while (_timestamps.Count > 0 && _timestamps.Peek() < windowStart)
                {
                    _timestamps.Dequeue();
                }

                if (_timestamps.Count >= _maxPerMinute)
                {
                    return false;
                }

                _timestamps.Enqueue(now);
                return true;
            }
        }
    }

    #endregion

    #region Helpers

    private async Task SaveSystemMessageAsync(long chatSessionId, string content, CancellationToken ct)
    {
        try
        {
            await _aiChatRepository.AddMessageAsync(new AIChatMessage
            {
                ChatSessionId = chatSessionId,
                SenderType = AIChatSenderType.SYSTEM.ToString(),
                Content = content,
                CreatedAt = DateTime.UtcNow
            }, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save SYSTEM message for session {SessionId}", chatSessionId);
        }
    }

    private async Task<bool> IsStudentActorAsync(int userId, string actorRole)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(actorRole))
        {
            return false;
        }

        if (!string.Equals(actorRole, UserRole.STUDENT.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var user = await _userRepository.GetUserByIdAsync(userId);
        return user is not null
            && user.IsActive
            && string.Equals(user.Role, UserRole.STUDENT.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSystemPrompt(string purpose)
    {
        const string grounding =
            "\n\nIMPORTANT RULES:\n" +
            "- Only draw conclusions from tool results. Never fabricate data.\n" +
            "- If data is missing or insufficient, clearly state what is unavailable.\n" +
            "- Do not reveal internal tool names or system architecture to the user.\n" +
            "- Always respond in a helpful, concise academic advising tone.";

        var basePrompt = purpose.ToUpperInvariant() switch
        {
            "SCORE_SUMMARY" =>
                "You are an academic assistant focused on score analysis, credits, risk indicators, and actionable improvement steps.",
            "STUDY_PLAN" =>
                "You are an academic assistant focused on creating short-term and mid-term study plans with concrete actions.",
            "COURSE_SUGGESTION" =>
                "You are an academic assistant focused on next-semester course suggestions with prerequisites and constraints awareness.",
            _ =>
                "You are an academic assistant."
        };

        return basePrompt + grounding;
    }

    private static AIChatSessionResponse MapSession(AIChatSession session)
    {
        return new AIChatSessionResponse
        {
            ChatSessionId = session.ChatSessionId,
            UserId = session.UserId,
            Purpose = session.Purpose,
            ModelName = session.ModelName,
            State = session.State,
            PromptVersion = session.PromptVersion,
            CreatedAtUtc = DateTime.SpecifyKind(session.CreatedAt, DateTimeKind.Utc),
            CompletedAtUtc = session.CompletedAt.HasValue
                ? DateTime.SpecifyKind(session.CompletedAt.Value, DateTimeKind.Utc)
                : null
        };
    }

    private static AIChatMessageResponse MapMessage(AIChatMessage message)
    {
        return new AIChatMessageResponse
        {
            ChatMessageId = message.ChatMessageId,
            ChatSessionId = message.ChatSessionId,
            SenderType = message.SenderType,
            Content = message.Content,
            CreatedAtUtc = DateTime.SpecifyKind(message.CreatedAt, DateTimeKind.Utc)
        };
    }

    private static AIToolCallResponse MapToolCall(AIToolCall toolCall)
    {
        return new AIToolCallResponse
        {
            ToolCallId = toolCall.ToolCallId,
            ChatSessionId = toolCall.ChatSessionId,
            ToolName = toolCall.ToolName,
            Status = toolCall.Status,
            CreatedAtUtc = DateTime.SpecifyKind(toolCall.CreatedAt, DateTimeKind.Utc)
        };
    }

    private static AIResponseSchemaDto? TryParseStructuredResponse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            var normalized = text.Trim();

            // Strip markdown code fences
            if (normalized.StartsWith("```"))
            {
                var firstNewline = normalized.IndexOf('\n');
                if (firstNewline > 0) normalized = normalized[(firstNewline + 1)..];
                if (normalized.EndsWith("```")) normalized = normalized[..^3];
                normalized = normalized.Trim();
            }

            var dto = JsonSerializer.Deserialize<AIResponseSchemaDto>(normalized, JsonCaseInsensitive);
            if (dto is null || string.IsNullOrWhiteSpace(dto.Purpose)) return null;

            return dto;
        }
        catch
        {
            return null;
        }
    }

    private static List<string> ValidateSchema(AIResponseSchemaDto dto)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(dto.Purpose))
        {
            errors.Add("Purpose is empty.");
        }

        foreach (var rf in dto.RiskFlags)
        {
            if (!string.IsNullOrWhiteSpace(rf.Severity) &&
                !ValidSeverities.Contains(rf.Severity.ToUpperInvariant()))
            {
                errors.Add($"RiskFlag severity invalid: {rf.Severity}");
                rf.Severity = "LOW";
            }
            else if (!string.IsNullOrWhiteSpace(rf.Severity))
            {
                rf.Severity = rf.Severity.ToUpperInvariant();
            }
        }

        foreach (var action in dto.RecommendedActions)
        {
            if (action.Priority < 1 || action.Priority > 5)
            {
                errors.Add($"Action priority out of range: {action.Priority}");
                action.Priority = Math.Clamp(action.Priority, 1, 5);
            }
        }

        foreach (var plan in dto.Plans)
        {
            if (plan.TotalCredits < 0)
            {
                errors.Add($"Plan TotalCredits negative: {plan.TotalCredits}");
                plan.TotalCredits = 0;
            }
        }

        return errors;
    }

    #endregion
}
