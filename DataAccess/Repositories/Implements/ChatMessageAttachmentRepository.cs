using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories.Implements;

public sealed class ChatMessageAttachmentRepository : IChatMessageAttachmentRepository
{
    private readonly SchoolManagementDbContext _context;

    public ChatMessageAttachmentRepository(SchoolManagementDbContext context)
    {
        _context = context;
    }

    public async Task<List<ChatMessageAttachment>> ListAttachmentsByMessageIdsAsync(IEnumerable<long> messageIds)
    {
        var ids = messageIds.ToList();
        if (ids.Count == 0) return new List<ChatMessageAttachment>();

        return await _context.ChatMessageAttachments
            .AsNoTracking()
            .Where(a => ids.Contains(a.MessageId))
            .OrderBy(a => a.AttachmentId)
            .ToListAsync();
    }
}
