﻿using BusinessLogic.DTOs.Requests.Chat;
using BusinessLogic.DTOs.Response;
using BusinessLogic.DTOs.Responses.Chat;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace BusinessLogic.Services.Implements;

public sealed class ChatService : IChatService
{
    private const int MaxMessageLength = 2000;
    private const int MaxRoomNameLength = 100;
    private const int DefaultPageSize = 20;
    private const int MaxUserSearchLimit = 20;

    // BANNED/REMOVED → no read access
    private static readonly HashSet<string> ReadBlockedStatuses = new(StringComparer.OrdinalIgnoreCase)
        { "BANNED", "REMOVED" };

    // BANNED/REMOVED/READ_ONLY/MUTED → no send access
    private static readonly HashSet<string> SendBlockedStatuses = new(StringComparer.OrdinalIgnoreCase)
        { "BANNED", "REMOVED", "READ_ONLY", "MUTED" };

    private readonly IChatRoomRepository _roomRepo;
    private readonly IChatRoomMemberRepository _memberRepo;
    private readonly IChatMessageRepository _messageRepo;
    private readonly IChatMessageAttachmentRepository _attachmentRepo;
    private readonly IChatModerationLogRepository _moderationRepo;
    private readonly IEnrollmentRepository _enrollmentRepo;
    private readonly IClassSectionRepository _classSectionRepo;
    private readonly IUserRepository _userRepo;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IChatRoomRepository roomRepo,
        IChatRoomMemberRepository memberRepo,
        IChatMessageRepository messageRepo,
        IChatMessageAttachmentRepository attachmentRepo,
        IChatModerationLogRepository moderationRepo,
        IEnrollmentRepository enrollmentRepo,
        IClassSectionRepository classSectionRepo,
        IUserRepository userRepo,
        ILogger<ChatService> logger)
    {
        _roomRepo = roomRepo;
        _memberRepo = memberRepo;
        _messageRepo = messageRepo;
        _attachmentRepo = attachmentRepo;
        _moderationRepo = moderationRepo;
        _enrollmentRepo = enrollmentRepo;
        _classSectionRepo = classSectionRepo;
        _userRepo = userRepo;
        _logger = logger;
    }

    #region Room Queries

    public async Task<List<ChatRoomDto>> GetMyRoomsAsync(int userId)
    {
        var rooms = await _roomRepo.ListRoomsForUserAsync(userId);
        
        var dmRoomIds = rooms.Where(r => r.RoomType == "DM").Select(r => r.RoomId).ToList();
        var dmNames = await _memberRepo.GetOtherMemberNamesForDmsAsync(dmRoomIds, userId);

        var result = new List<ChatRoomDto>();
        foreach(var room in rooms)
        {
            var membership = await _memberRepo.GetMembershipAsync(room.RoomId, userId);
            var mapped = MapRoom(room, membership);
            
            if (mapped.RoomType == "DM" && dmNames.TryGetValue(room.RoomId, out var otherName))
            {
                mapped.RoomName = otherName;
            }

            result.Add(mapped);
        }
        return result;
    }

    public async Task<ChatRoomDto?> GetRoomAsync(int roomId, int userId)
    {
        var membership = await _memberRepo.GetMembershipAsync(roomId, userId);
        if (membership is null || IsReadBlocked(membership))
            return null;

        var room = await _roomRepo.GetRoomByIdAsync(roomId);
        if (room is null) return null;
        
        var mapped = MapRoom(room, membership);
        if (mapped.RoomType == "DM")
        {
            var dmNames = await _memberRepo.GetOtherMemberNamesForDmsAsync(new[] { roomId }, userId);
            if (dmNames.TryGetValue(roomId, out var otherName))
            {
                mapped.RoomName = otherName;
            }
        }
        return mapped;
    }

    #endregion

    #region Message Queries

    public async Task<PagedResult<ChatMessageDto>> GetRoomMessagesAsync(
        int roomId, int userId, long? beforeMessageId, int pageSize = DefaultPageSize)
    {
        var membership = await _memberRepo.GetMembershipAsync(roomId, userId);
        if (membership is null || IsReadBlocked(membership))
            return EmptyMessages();

        if (pageSize <= 0) pageSize = DefaultPageSize;

        var messages = await _messageRepo.GetMessagesAsync(roomId, beforeMessageId, pageSize);

        // Batch-load attachments to avoid N+1
        var messageIds = messages.Select(m => m.MessageId).ToList();
        var attachments = messageIds.Count > 0
            ? await _attachmentRepo.ListAttachmentsByMessageIdsAsync(messageIds)
            : new List<ChatMessageAttachment>();

        var attachmentsByMsg = attachments
            .GroupBy(a => a.MessageId)
            .ToDictionary(g => g.Key, g => g.Select(MapAttachment).ToList());

        var items = messages.Select(m => MapMessage(m, attachmentsByMsg)).ToList();

        return new PagedResult<ChatMessageDto>
        {
            Page = 1,
            PageSize = pageSize,
            TotalCount = items.Count,
            Items = items
        };
    }

    public async Task<ChatMessageDto?> GetLatestMessageAsync(int roomId, int userId)
    {
        var membership = await _memberRepo.GetMembershipAsync(roomId, userId);
        if (membership is null || IsReadBlocked(membership))
            return null;

        var message = await _messageRepo.GetLatestMessageAsync(roomId);
        if (message is null) return null;

        var attachments = message.ChatMessageAttachments
            .Select(MapAttachment).ToList();

        var dto = MapMessage(message);
        dto.Attachments = attachments;
        return dto;
    }

    #endregion

    #region Message Commands

    public async Task<OperationResult> SendMessageAsync(
        int roomId, int userId, string? content, List<ChatAttachmentInput>? attachments)
    {
        var room = await _roomRepo.GetRoomByIdAsync(roomId);
        if (room is null)
            return OperationResult.Fail("Room not found.", "NOT_FOUND");

        // Auto-join for CLASS/COURSE if eligible
        var membership = await EnsureMembership(room, userId);
        if (membership is null)
            return OperationResult.Fail("You are not a member of this room.", "FORBIDDEN");

        if (IsReadBlocked(membership))
            return OperationResult.Fail("You cannot access this room.", "FORBIDDEN");

        // MUTED user → log moderation event before rejecting
        if (IsSendBlocked(membership))
        {
            if (membership.MemberStatus.Equals("MUTED", StringComparison.OrdinalIgnoreCase))
            {
                await _moderationRepo.InsertModerationLogAsync(new ChatModerationLog
                {
                    RoomId = roomId,
                    ActorUserId = userId,
                    Action = "MUTED_SEND_BLOCKED",
                    TargetUserId = userId,
                    CreatedAt = DateTime.UtcNow
                });
                _logger.LogWarning("User {UserId} tried to send in room {RoomId} while MUTED", userId, roomId);
            }
            return OperationResult.Fail("You are not allowed to send messages in this room.", "FORBIDDEN");
        }

        // LOCKED room → only OWNER/MODERATOR may send
        if (room.Status.Equals("LOCKED", StringComparison.OrdinalIgnoreCase))
        {
            if (!IsOwnerOrModerator(membership))
                return OperationResult.Fail("Room is locked. Only OWNER or MODERATOR can send.", "FORBIDDEN");
        }

        if (string.IsNullOrWhiteSpace(content) && (attachments is null || attachments.Count == 0))
            return OperationResult.Fail("Message content or attachment is required.", "VALIDATION_ERROR");

        if (content is not null && content.Length > MaxMessageLength)
            return OperationResult.Fail($"Message exceeds max length ({MaxMessageLength}).", "VALIDATION_ERROR");

        var message = new ChatMessage
        {
            RoomId = roomId,
            SenderId = userId,
            MessageType = "TEXT",
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        // Skip attachment items missing FileUrl or FileType
        var attachmentEntities = BuildAttachmentEntities(attachments);

        if (attachmentEntities.Count > 0)
            await _messageRepo.InsertMessageWithAttachmentsAsync(message, attachmentEntities);
        else
            await _messageRepo.InsertMessageAsync(message);

        _logger.LogInformation("User {UserId} sent message {MessageId} in room {RoomId}", userId, message.MessageId, roomId);
        return OperationResult.Ok("Message sent.");
    }

    public async Task<OperationResult> EditMessageAsync(int roomId, long messageId, int userId, string? newContent)
    {
        if (string.IsNullOrWhiteSpace(newContent))
            return OperationResult.Fail("Content cannot be empty.", "VALIDATION_ERROR");

        if (newContent.Length > MaxMessageLength)
            return OperationResult.Fail($"Message exceeds max length ({MaxMessageLength}).", "VALIDATION_ERROR");

        var message = await _messageRepo.GetMessageByIdAsync(messageId);
        if (message is null || message.RoomId != roomId)
            return OperationResult.Fail("Message not found.", "NOT_FOUND");

        // Only sender can edit their own message
        if (message.SenderId != userId)
            return OperationResult.Fail("You can only edit your own messages.", "FORBIDDEN");

        if (message.DeletedAt.HasValue)
            return OperationResult.Fail("Cannot edit a deleted message.", "VALIDATION_ERROR");

        message.Content = newContent;
        message.EditedAt = DateTime.UtcNow;
        await _messageRepo.UpdateMessageAsync(message);

        _logger.LogInformation("User {UserId} edited message {MessageId}", userId, messageId);
        return OperationResult.Ok("Message edited.");
    }

    public async Task<OperationResult> DeleteMessageAsync(int roomId, long messageId, int userId)
    {
        var message = await _messageRepo.GetMessageByIdAsync(messageId);
        if (message is null || message.RoomId != roomId)
            return OperationResult.Fail("Message not found.", "NOT_FOUND");

        if (message.DeletedAt.HasValue)
            return OperationResult.Fail("Message already deleted.", "VALIDATION_ERROR");

        var isSender = message.SenderId == userId;

        if (!isSender)
        {
            return OperationResult.Fail("You can only delete your own messages.", "FORBIDDEN");
        }

        await _messageRepo.SoftDeleteMessageAsync(messageId, DateTime.UtcNow);

        _logger.LogInformation("User {UserId} deleted message {MessageId}", userId, messageId);
        return OperationResult.Ok("Message deleted.");
    }

    public async Task<OperationResult> MarkReadAsync(int roomId, int userId, long lastReadMessageId)
    {
        var membership = await _memberRepo.GetMembershipAsync(roomId, userId);
        if (membership is null || IsReadBlocked(membership))
            return OperationResult.Fail("You do not have access to this room.", "FORBIDDEN");

        await _memberRepo.UpdateLastReadMessageIdAsync(roomId, userId, lastReadMessageId);
        return OperationResult.Ok();
    }

    #endregion

    #region Room Commands

    public async Task<OperationResult<ChatRoomDto>> CreateGroupRoomAsync(
        int creatorUserId, string? roomName, List<int>? memberUserIds)
    {
        if (string.IsNullOrWhiteSpace(roomName))
            return OperationResult.Fail<ChatRoomDto>("Room name is required.", "VALIDATION_ERROR");

        if (roomName.Length > MaxRoomNameLength)
            return OperationResult.Fail<ChatRoomDto>($"Room name exceeds max length ({MaxRoomNameLength}).", "VALIDATION_ERROR");

        var room = new ChatRoom
        {
            RoomType = "GROUP",
            RoomName = roomName,
            Status = "ACTIVE",
            CreatedBy = creatorUserId,
            CreatedAt = DateTime.UtcNow
        };

        room = await _roomRepo.CreateRoomAsync(room);

        // Creator → OWNER; others → MEMBER
        await _memberRepo.UpsertMembershipAsync(room.RoomId, creatorUserId, "OWNER", "JOINED");

        if (memberUserIds is not null)
        {
            foreach (var memberId in memberUserIds.Distinct().Where(id => id != creatorUserId))
            {
                await _memberRepo.UpsertMembershipAsync(room.RoomId, memberId, "MEMBER", "JOINED");
            }
        }

        _logger.LogInformation("User {UserId} created GROUP room {RoomId} '{RoomName}'", creatorUserId, room.RoomId, roomName);
        return OperationResult.Ok(MapRoom(room));
    }

    public async Task<OperationResult<ChatRoomDto>> CreateOrGetDmRoomAsync(int userId, int otherUserId)
    {
        if (userId == otherUserId)
            return OperationResult.Fail<ChatRoomDto>("Cannot create a DM room with yourself.", "VALIDATION_ERROR");

        var otherUser = await _userRepo.GetUserByIdAsync(otherUserId);
        if (otherUser is null || !otherUser.IsActive)
            return OperationResult.Fail<ChatRoomDto>("Other user not found or inactive.", "NOT_FOUND");

        // Return existing DM if one already exists between the two users
        var existing = await _roomRepo.GetDmRoomAsync(userId, otherUserId);
        if (existing is not null)
        {
            var existingMapped = MapRoom(existing);
            existingMapped.RoomName = otherUser.FullName;
            return OperationResult.Ok(existingMapped);
        }

        var room = new ChatRoom
        {
            RoomType = "DM",
            RoomName = "DM",
            Status = "ACTIVE",
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow
        };

        room = await _roomRepo.CreateRoomAsync(room);

        await _memberRepo.UpsertMembershipAsync(room.RoomId, userId, "MEMBER", "JOINED");
        await _memberRepo.UpsertMembershipAsync(room.RoomId, otherUserId, "MEMBER", "JOINED");

        _logger.LogInformation("User {UserId} created DM room {RoomId} with user {OtherUserId}", userId, room.RoomId, otherUserId);
        
        var mapped = MapRoom(room);
        mapped.RoomName = otherUser.FullName;
        return OperationResult.Ok(mapped);
    }

    #endregion

    #region User Discovery

    public async Task<List<AvailableUserDto>> GetAvailableUsersForChatAsync(int userId, string? search)
    {
        var users = await _userRepo.SearchActiveUsersAsync(search, userId, MaxUserSearchLimit);
        return users.Select(u => new AvailableUserDto
        {
            UserId = u.UserId,
            FullName = u.FullName,
            Role = u.Role,
            Email = u.Email
        }).ToList();
    }

    #endregion

    #region Auto-membership

    public async Task<OperationResult> EnsureClassChatMembershipAsync(int classSectionId, int studentId)
    {
        var room = await _roomRepo.GetRoomByTypeAndRefAsync("CLASS", classSectionId, null);
        if (room is null)
            return OperationResult.Fail("No CLASS chat room exists for this class section.", "NOT_FOUND");

        var membership = await _memberRepo.GetMembershipAsync(room.RoomId, studentId);

        if (membership is null)
        {
            await _memberRepo.UpsertMembershipAsync(room.RoomId, studentId, "MEMBER", "JOINED");
            _logger.LogInformation("Student {StudentId} auto-joined CLASS room {RoomId}", studentId, room.RoomId);
            return OperationResult.Ok("Joined class chat room.");
        }

        // Restore access if previously set to READ_ONLY or REMOVED
        if (membership.MemberStatus.Equals("READ_ONLY", StringComparison.OrdinalIgnoreCase)
            || membership.MemberStatus.Equals("REMOVED", StringComparison.OrdinalIgnoreCase))
        {
            await _memberRepo.UpdateMemberStatusAsync(room.RoomId, studentId, "JOINED");
            _logger.LogInformation("Student {StudentId} status updated to JOINED in CLASS room {RoomId}", studentId, room.RoomId);
            return OperationResult.Ok("Membership status updated to JOINED.");
        }

        return OperationResult.Ok("Already a member.");
    }

    #endregion

    #region Private Helpers

    /// <summary>
    /// Auto-join for CLASS/COURSE rooms if user is eligible. Returns null if no access.
    /// </summary>
    private async Task<ChatRoomMember?> EnsureMembership(ChatRoom room, int userId)
    {
        var membership = await _memberRepo.GetMembershipAsync(room.RoomId, userId);
        if (membership is not null)
            return membership;

        // Auto-join only for CLASS/COURSE
        if (!room.RoomType.Equals("CLASS", StringComparison.OrdinalIgnoreCase)
            && !room.RoomType.Equals("COURSE", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var user = await _userRepo.GetUserByIdAsync(userId);
        if (user is null || !user.IsActive)
            return null;

        bool canJoin;

        if (room.RoomType.Equals("CLASS", StringComparison.OrdinalIgnoreCase))
            canJoin = await CanAccessClassRoom(user, room);
        else
            canJoin = await CanAccessCourseRoom(user, room);

        if (!canJoin) return null;

        return await _memberRepo.UpsertMembershipAsync(room.RoomId, userId, "MEMBER", "JOINED");
    }

    /// <summary>ADMIN=ok, TEACHER=assigned to section, STUDENT=enrolled in section.</summary>
    private async Task<bool> CanAccessClassRoom(User user, ChatRoom room)
    {
        if (!room.ClassSectionId.HasValue) return false;

        if (user.Role.Equals("ADMIN", StringComparison.OrdinalIgnoreCase))
            return true;

        if (user.Role.Equals("TEACHER", StringComparison.OrdinalIgnoreCase))
            return await _classSectionRepo.IsTeacherAssignedAsync(user.UserId, room.ClassSectionId.Value);

        if (user.Role.Equals("STUDENT", StringComparison.OrdinalIgnoreCase))
            return await _enrollmentRepo.IsStudentEnrolledAsync(user.UserId, room.ClassSectionId.Value);

        return false;
    }

    /// <summary>ADMIN=ok, TEACHER=assigned to course, STUDENT=enrolled in course.</summary>
    private async Task<bool> CanAccessCourseRoom(User user, ChatRoom room)
    {
        if (!room.CourseId.HasValue) return false;

        if (user.Role.Equals("ADMIN", StringComparison.OrdinalIgnoreCase))
            return true;

        if (user.Role.Equals("TEACHER", StringComparison.OrdinalIgnoreCase))
            return await _classSectionRepo.IsTeacherAssignedToCourseAsync(user.UserId, room.CourseId.Value);

        if (user.Role.Equals("STUDENT", StringComparison.OrdinalIgnoreCase))
            return await _enrollmentRepo.IsStudentEnrolledInCourseAsync(user.UserId, room.CourseId.Value);

        return false;
    }

    private static bool IsReadBlocked(ChatRoomMember m) => ReadBlockedStatuses.Contains(m.MemberStatus);
    private static bool IsSendBlocked(ChatRoomMember m) => SendBlockedStatuses.Contains(m.MemberStatus);
    private static bool IsOwnerOrModerator(ChatRoomMember m)
        => m.RoleInRoom.Equals("OWNER", StringComparison.OrdinalIgnoreCase)
           || m.RoleInRoom.Equals("MODERATOR", StringComparison.OrdinalIgnoreCase);

    /// <summary>Skips items with missing FileUrl or FileType.</summary>
    private static List<ChatMessageAttachment> BuildAttachmentEntities(List<ChatAttachmentInput>? inputs)
    {
        if (inputs is null || inputs.Count == 0)
            return new List<ChatMessageAttachment>();

        return inputs
            .Where(a => !string.IsNullOrWhiteSpace(a.FileUrl) && !string.IsNullOrWhiteSpace(a.FileType))
            .Select(a => new ChatMessageAttachment
            {
                FileUrl = a.FileUrl!,
                FileType = a.FileType!,
                FileSizeBytes = a.FileSizeBytes,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();
    }

    private static PagedResult<ChatMessageDto> EmptyMessages() => new()
    {
        Page = 1,
        PageSize = DefaultPageSize,
        TotalCount = 0,
        Items = Array.Empty<ChatMessageDto>()
    };

    #endregion

    #region Mapping

    private static ChatRoomDto MapRoom(ChatRoom r, ChatRoomMember? m = null) => new()
    {
        RoomId = r.RoomId,
        RoomType = r.RoomType,
        CourseId = r.CourseId,
        ClassSectionId = r.ClassSectionId,
        RoomName = r.RoomName,
        Status = r.Status,
        CreatedBy = r.CreatedBy,
        CreatedAt = r.CreatedAt,
        CurrentUserRole = m?.RoleInRoom,
        CurrentMemberStatus = m?.MemberStatus
    };

    private static ChatMessageDto MapMessage(ChatMessage m,
        Dictionary<long, List<ChatAttachmentDto>>? attachmentsByMsg = null) => new()
    {
        MessageId = m.MessageId,
        RoomId = m.RoomId,
        SenderId = m.SenderId,
        SenderName = m.Sender?.FullName ?? $"User #{m.SenderId}",
        MessageType = m.MessageType,
        Content = m.Content,
        CreatedAt = m.CreatedAt,
        EditedAt = m.EditedAt,
        DeletedAt = m.DeletedAt,
        Attachments = attachmentsByMsg is not null && attachmentsByMsg.TryGetValue(m.MessageId, out var atts)
            ? atts
            : m.ChatMessageAttachments.Select(MapAttachment).ToList()
    };

    private static ChatAttachmentDto MapAttachment(ChatMessageAttachment a) => new()
    {
        AttachmentId = a.AttachmentId,
        MessageId = a.MessageId,
        FileUrl = a.FileUrl,
        FileType = a.FileType,
        FileSizeBytes = a.FileSizeBytes,
        CreatedAt = a.CreatedAt,
        OriginalName = Path.GetFileName(a.FileUrl)
    };

    #endregion
}
