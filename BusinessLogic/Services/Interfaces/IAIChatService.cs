using BusinessLogic.DTOs.Requests.AI;
using BusinessLogic.DTOs.Response;
using BusinessLogic.DTOs.Responses.AI;

namespace BusinessLogic.Services.Interfaces;

public interface IAIChatService
{
    Task<ServiceResult<AIChatSessionResponse>> StartSessionAsync(
        int userId,
        string actorRole,
        StartChatSessionRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<AIChatTurnResponse>> SendMessageAsync(
        int userId,
        string actorRole,
        SendChatMessageRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<PagedResultDto>> GetSessionHistoryAsync(
        int userId,
        string actorRole,
        long chatSessionId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);
}
