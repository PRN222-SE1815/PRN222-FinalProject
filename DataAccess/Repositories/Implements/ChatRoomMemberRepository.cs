using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories.Implements;

public sealed class ChatRoomMemberRepository : IChatRoomMemberRepository
{
    private readonly SchoolManagementDbContext _context;

    public ChatRoomMemberRepository(SchoolManagementDbContext context)
    {
        _context = context;
    }

    public async Task<ChatRoomMember?> GetMembershipAsync(int roomId, int userId)
    {
        return await _context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId);
    }

    public async Task<ChatRoomMember> UpsertMembershipAsync(int roomId, int userId, string roleInRoom, string memberStatus)
    {
        var existing = await _context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId);

        if (existing is not null)
        {
            existing.RoleInRoom = roleInRoom;
            existing.MemberStatus = memberStatus;
            await _context.SaveChangesAsync();
            return existing;
        }

        var member = new ChatRoomMember
        {
            RoomId = roomId,
            UserId = userId,
            RoleInRoom = roleInRoom,
            MemberStatus = memberStatus,
            JoinedAt = DateTime.UtcNow
        };
        _context.ChatRoomMembers.Add(member);
        await _context.SaveChangesAsync();
        return member;
    }

    public async Task UpdateLastReadMessageIdAsync(int roomId, int userId, long lastReadMessageId)
    {
        var member = await _context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId);

        if (member is not null)
        {
            member.LastReadMessageId = lastReadMessageId;
            await _context.SaveChangesAsync();
        }
    }

    public async Task UpdateMemberStatusAsync(int roomId, int userId, string newStatus)
    {
        var member = await _context.ChatRoomMembers
            .FirstOrDefaultAsync(m => m.RoomId == roomId && m.UserId == userId);

        if (member is not null)
        {
            member.MemberStatus = newStatus;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<Dictionary<int, string>> GetOtherMemberNamesForDmsAsync(IEnumerable<int> dmRoomIds, int currentUserId)
    {
        var dict = new Dictionary<int, string>();
        if (!dmRoomIds.Any()) return dict;

        var otherMembers = await _context.ChatRoomMembers
            .Include(m => m.User)
            .Where(m => dmRoomIds.Contains(m.RoomId) && m.UserId != currentUserId)
            .ToListAsync();

        foreach (var m in otherMembers)
        {
            if (m.User != null)
            {
                // In case of multiple other members unexpectedly, we take the first one
                dict.TryAdd(m.RoomId, m.User.FullName);
            }
        }

        return dict;
    }
}
