using BusinessLogic.DTOs.Response;
using BusinessLogic.DTOs.Responses.AI;

namespace BusinessLogic.Services.Interfaces;

public interface IGeminiClientService
{
    Task<ServiceResult<string>> GenerateAsync(
        string model,
        string systemPrompt,
        IReadOnlyList<AIChatMessageResponse> messages,
        CancellationToken ct = default);

    Task<ServiceResult<GeminiToolDecisionDto>> GenerateWithToolsAsync(
        string model,
        string systemPrompt,
        IReadOnlyList<AIChatMessageResponse> messages,
        IReadOnlyList<string> toolSchemasJson,
        CancellationToken ct = default);
}
