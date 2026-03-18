using BusinessObject.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface IChatMessageRepository
{
    /// <summary>
    /// Get messages for a room with cursor-based paging (before a given messageId).
    /// Returns up to <paramref name="pageSize"/> messages ordered by CreatedAt desc.
    /// </summary>
    Task<List<ChatMessage>> GetMessagesAsync(int roomId, long? beforeMessageId, int pageSize);

    /// <summary>
    /// Get the latest message in a room, including attachments.
    /// </summary>
    Task<ChatMessage?> GetLatestMessageAsync(int roomId);

    Task<ChatMessage?> GetMessageByIdAsync(long messageId);

    /// <summary>
    /// Insert a message (without attachments).
    /// </summary>
    Task<ChatMessage> InsertMessageAsync(ChatMessage message);

    /// <summary>
    /// Insert a message together with its attachments in one operation.
    /// </summary>
    Task<ChatMessage> InsertMessageWithAttachmentsAsync(ChatMessage message, IEnumerable<ChatMessageAttachment> attachments);

    /// <summary>
    /// Update message content / EditedAt.
    /// </summary>
    Task UpdateMessageAsync(ChatMessage message);

    /// <summary>
    /// Soft-delete: set DeletedAt on the message.
    /// </summary>
    Task SoftDeleteMessageAsync(long messageId, DateTime deletedAt);
}
