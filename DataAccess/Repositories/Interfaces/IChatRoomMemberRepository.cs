using BusinessObject.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface IChatRoomMemberRepository
{
    Task<ChatRoomMember?> GetMembershipAsync(int roomId, int userId);
    Task<ChatRoomMember> UpsertMembershipAsync(int roomId, int userId, string roleInRoom, string memberStatus);
    Task UpdateLastReadMessageIdAsync(int roomId, int userId, long lastReadMessageId);
    Task UpdateMemberStatusAsync(int roomId, int userId, string newStatus);
    Task<Dictionary<int, string>> GetOtherMemberNamesForDmsAsync(IEnumerable<int> dmRoomIds, int currentUserId);
}
