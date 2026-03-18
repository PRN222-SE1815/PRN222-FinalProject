using BusinessObject.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface IAIChatRepository
{
    Task<AIChatSession> CreateSessionAsync(AIChatSession session, CancellationToken ct = default);
    Task<AIChatSession?> GetSessionByIdAsync(long chatSessionId, CancellationToken ct = default);
    Task<AIChatSession?> GetSessionWithUserAsync(long chatSessionId, CancellationToken ct = default);

    Task<(IReadOnlyList<AIChatSession> Items, int TotalCount)> GetSessionsByUserAsync(
        int userId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<AIChatMessage> AddMessageAsync(AIChatMessage message, CancellationToken ct = default);

    Task<(IReadOnlyList<AIChatMessage> Items, int TotalCount)> GetMessagesBySessionAsync(
        long chatSessionId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<AIToolCall> AddToolCallAsync(AIToolCall toolCall, CancellationToken ct = default);

    Task<IReadOnlyList<AIToolCall>> GetToolCallsBySessionAsync(
        long chatSessionId,
        CancellationToken ct = default);

    void UpdateSession(AIChatSession session);

    Task SaveChangesAsync(CancellationToken ct = default);
}
