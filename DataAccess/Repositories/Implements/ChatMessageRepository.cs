using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories.Implements;

public sealed class ChatMessageRepository : IChatMessageRepository
{
    private readonly SchoolManagementDbContext _context;

    public ChatMessageRepository(SchoolManagementDbContext context)
    {
        _context = context;
    }

    public async Task<List<ChatMessage>> GetMessagesAsync(int roomId, long? beforeMessageId, int pageSize)
    {
        var query = _context.ChatMessages
            .AsNoTracking()
            .Include(m => m.ChatMessageAttachments)
            .Include(m => m.Sender)
            .Where(m => m.RoomId == roomId);

        if (beforeMessageId.HasValue)
        {
            query = query.Where(m => m.MessageId < beforeMessageId.Value);
        }

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.MessageId)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<ChatMessage?> GetLatestMessageAsync(int roomId)
    {
        return await _context.ChatMessages
            .AsNoTracking()
            .Include(m => m.ChatMessageAttachments)
            .Include(m => m.Sender)
            .Where(m => m.RoomId == roomId)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.MessageId)
            .FirstOrDefaultAsync();
    }

    public async Task<ChatMessage?> GetMessageByIdAsync(long messageId)
    {
        return await _context.ChatMessages
            .Include(m => m.ChatMessageAttachments)
            .Include(m => m.Sender)
            .FirstOrDefaultAsync(m => m.MessageId == messageId);
    }

    public async Task<ChatMessage> InsertMessageAsync(ChatMessage message)
    {
        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();
        return message;
    }

    public async Task<ChatMessage> InsertMessageWithAttachmentsAsync(ChatMessage message, IEnumerable<ChatMessageAttachment> attachments)
    {
        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync(); // get MessageId

        foreach (var att in attachments)
        {
            att.MessageId = message.MessageId;
            _context.ChatMessageAttachments.Add(att);
        }

        await _context.SaveChangesAsync();
        return message;
    }

    public async Task UpdateMessageAsync(ChatMessage message)
    {
        _context.ChatMessages.Update(message);
        await _context.SaveChangesAsync();
    }

    public async Task SoftDeleteMessageAsync(long messageId, DateTime deletedAt)
    {
        var message = await _context.ChatMessages.FindAsync(messageId);
        if (message is not null)
        {
            message.DeletedAt = deletedAt;
            await _context.SaveChangesAsync();
        }
    }
}
