using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories.Implements;

public sealed class ChatRoomRepository : IChatRoomRepository
{
    private readonly SchoolManagementDbContext _context;

    public ChatRoomRepository(SchoolManagementDbContext context)
    {
        _context = context;
    }

    public async Task<ChatRoom?> GetRoomByIdAsync(int roomId)
    {
        return await _context.ChatRooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoomId == roomId);
    }

    public async Task<List<ChatRoom>> ListRoomsForUserAsync(int userId)
    {
        // Return rooms where the user is a member and not BANNED/REMOVED
        return await _context.ChatRoomMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId
                        && m.MemberStatus != "BANNED"
                        && m.MemberStatus != "REMOVED")
            .Select(m => m.Room)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
    }

    public async Task<ChatRoom> CreateRoomAsync(ChatRoom room)
    {
        _context.ChatRooms.Add(room);
        await _context.SaveChangesAsync();
        return room;
    }

    public async Task<ChatRoom?> GetRoomByTypeAndRefAsync(string roomType, int? classSectionId, int? courseId)
    {
        return await _context.ChatRooms
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RoomType == roomType
                                      && r.ClassSectionId == classSectionId
                                      && r.CourseId == courseId);
    }

    public async Task<ChatRoom?> GetDmRoomAsync(int userId1, int userId2)
    {
        // A DM room has exactly 2 members: userId1 and userId2
        return await _context.ChatRooms
            .AsNoTracking()
            .Where(r => r.RoomType == "DM")
            .Where(r => r.ChatRoomMembers.Any(m => m.UserId == userId1)
                        && r.ChatRoomMembers.Any(m => m.UserId == userId2)
                        && r.ChatRoomMembers.Count == 2)
            .FirstOrDefaultAsync();
    }
}
