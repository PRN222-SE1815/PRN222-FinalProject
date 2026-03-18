using BusinessObject.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface IChatMessageAttachmentRepository
{
    /// <summary>
    /// Get attachments for a batch of message IDs.
    /// </summary>
    Task<List<ChatMessageAttachment>> ListAttachmentsByMessageIdsAsync(IEnumerable<long> messageIds);
}
