using BusinessObject.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface IChatRoomRepository
{
    Task<ChatRoom?> GetRoomByIdAsync(int roomId);
    Task<List<ChatRoom>> ListRoomsForUserAsync(int userId);
    Task<ChatRoom> CreateRoomAsync(ChatRoom room);

    /// <summary>
    /// Find a room by type + reference id (ClassSectionId or CourseId).
    /// For DM: use roomType=DM, refId is not used – use separate DM lookup.
    /// For CLASS: refId = classSectionId.
    /// For COURSE: refId = courseId.
    /// </summary>
    Task<ChatRoom?> GetRoomByTypeAndRefAsync(string roomType, int? classSectionId, int? courseId);

    /// <summary>
    /// Find an existing DM room between exactly two users.
    /// </summary>
    Task<ChatRoom?> GetDmRoomAsync(int userId1, int userId2);
}
