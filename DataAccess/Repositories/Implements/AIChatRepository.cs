using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataAccess.Repositories.Implements;

public sealed class AIChatRepository : IAIChatRepository
{
    private readonly SchoolManagementDbContext _context;
    private readonly ILogger<AIChatRepository> _logger;

    public AIChatRepository(SchoolManagementDbContext context, ILogger<AIChatRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<AIChatSession> CreateSessionAsync(AIChatSession session, CancellationToken ct = default)
    {
        await _context.AIChatSessions.AddAsync(session, ct);
        await _context.SaveChangesAsync(ct);
        return session;
    }

    public Task<AIChatSession?> GetSessionByIdAsync(long chatSessionId, CancellationToken ct = default)
    {
        return _context.AIChatSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ChatSessionId == chatSessionId, ct);
    }

    public Task<AIChatSession?> GetSessionWithUserAsync(long chatSessionId, CancellationToken ct = default)
    {
        return _context.AIChatSessions
            .AsNoTracking()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.ChatSessionId == chatSessionId, ct);
    }

    public async Task<(IReadOnlyList<AIChatSession> Items, int TotalCount)> GetSessionsByUserAsync(
        int userId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : pageSize;

        var query = _context.AIChatSessions
            .AsNoTracking()
            .Where(s => s.UserId == userId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(s => s.CreatedAt)
            .ThenByDescending(s => s.ChatSessionId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<AIChatMessage> AddMessageAsync(AIChatMessage message, CancellationToken ct = default)
    {
        await _context.AIChatMessages.AddAsync(message, ct);
        await _context.SaveChangesAsync(ct);
        return message;
    }

    public async Task<(IReadOnlyList<AIChatMessage> Items, int TotalCount)> GetMessagesBySessionAsync(
        long chatSessionId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : pageSize;

        var query = _context.AIChatMessages
            .AsNoTracking()
            .Where(m => m.ChatSessionId == chatSessionId);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.ChatMessageId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<AIToolCall> AddToolCallAsync(AIToolCall toolCall, CancellationToken ct = default)
    {
        await _context.AIToolCalls.AddAsync(toolCall, ct);
        await _context.SaveChangesAsync(ct);
        return toolCall;
    }

    public async Task<IReadOnlyList<AIToolCall>> GetToolCallsBySessionAsync(long chatSessionId, CancellationToken ct = default)
    {
        return await _context.AIToolCalls
            .AsNoTracking()
            .Where(t => t.ChatSessionId == chatSessionId)
            .OrderBy(t => t.CreatedAt)
            .ThenBy(t => t.ToolCallId)
            .ToListAsync(ct);
    }

    public void UpdateSession(AIChatSession session)
    {
        _context.AIChatSessions.Update(session);
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }
}
