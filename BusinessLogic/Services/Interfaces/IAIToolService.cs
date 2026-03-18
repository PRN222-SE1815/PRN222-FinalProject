using BusinessLogic.DTOs.Response;

namespace BusinessLogic.Services.Interfaces;

public interface IAIToolService
{
    Task<ServiceResult<string>> ExecuteToolAsync(
        int userId,
        string actorRole,
        string toolName,
        string toolArgumentsJson,
        CancellationToken ct = default);

    IReadOnlyList<string> GetSupportedToolNames();
}
