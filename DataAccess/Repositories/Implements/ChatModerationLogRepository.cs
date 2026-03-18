using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;

namespace DataAccess.Repositories.Implements;

public sealed class ChatModerationLogRepository : IChatModerationLogRepository
{
    private readonly SchoolManagementDbContext _context;

    public ChatModerationLogRepository(SchoolManagementDbContext context)
    {
        _context = context;
    }

    public async Task InsertModerationLogAsync(ChatModerationLog log)
    {
        _context.ChatModerationLogs.Add(log);
        await _context.SaveChangesAsync();
    }
}
