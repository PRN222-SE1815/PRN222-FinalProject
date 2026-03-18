using BusinessLogic.DTOs.Requests.Chat;
using BusinessLogic.DTOs.Response;
using BusinessLogic.DTOs.Responses.Chat;

namespace BusinessLogic.Services.Interfaces;

public interface IChatService
{
    // ==================== Room Queries ====================

    /// <summary>Get rooms the user is a member of (excluding BANNED/REMOVED).</summary>
    Task<List<ChatRoomDto>> GetMyRoomsAsync(int userId);

    /// <summary>Get room info if the user has access, otherwise null.</summary>
    Task<ChatRoomDto?> GetRoomAsync(int roomId, int userId);

    // ==================== Message Queries ====================

    /// <summary>
    /// Get messages in a room with cursor-based paging.
    /// Returns PagedResult with page=1, totalCount=items.Count; default pageSize=20.
    /// </summary>
    Task<PagedResult<ChatMessageDto>> GetRoomMessagesAsync(int roomId, int userId, long? beforeMessageId, int pageSize = 20);

    /// <summary>Get the latest message in a room (with attachments), or null.</summary>
    Task<ChatMessageDto?> GetLatestMessageAsync(int roomId, int userId);

    // ==================== Message Commands ====================

    Task<OperationResult> SendMessageAsync(int roomId, int userId, string? content, List<ChatAttachmentInput>? attachments);
    Task<OperationResult> EditMessageAsync(int roomId, long messageId, int userId, string? newContent);
    Task<OperationResult> DeleteMessageAsync(int roomId, long messageId, int userId);
    Task<OperationResult> MarkReadAsync(int roomId, int userId, long lastReadMessageId);

    // ==================== Room Commands ====================

    Task<OperationResult<ChatRoomDto>> CreateGroupRoomAsync(int creatorUserId, string? roomName, List<int>? memberUserIds);
    Task<OperationResult<ChatRoomDto>> CreateOrGetDmRoomAsync(int userId, int otherUserId);

    // ==================== User Discovery ====================

    /// <summary>Search active users for chat (limit 20, excludes current user).</summary>
    Task<List<AvailableUserDto>> GetAvailableUsersForChatAsync(int userId, string? search);

    // ==================== Auto-membership ====================

    /// <summary>
    /// Ensure a student is a member of the CLASS chat room for a class section.
    /// If room exists and student not yet member → JOINED.
    /// If status is READ_ONLY or REMOVED → set to JOINED.
    /// </summary>
    Task<OperationResult> EnsureClassChatMembershipAsync(int classSectionId, int studentId);
}
