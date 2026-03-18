using System.Security.Claims;
using BusinessLogic.DTOs.Requests.Chat;
using BusinessLogic.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Presentation.Hubs;

/// <summary>
/// SignalR hub for real-time chat.
/// Group naming convention: "room:{roomId}"
/// </summary>
[Authorize]
public sealed class ChatHub : Hub
{
    private readonly IChatService _chatService;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IChatService chatService, ILogger<ChatHub> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    // ==================== Connection ====================

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId == 0)
        {
            Context.Abort();
            return;
        }

        // Auto-join all rooms the user is a member of
        var rooms = await _chatService.GetMyRoomsAsync(userId);
        foreach (var room in rooms)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{room.RoomId}");
        }

        _logger.LogInformation("User {UserId} connected to ChatHub ({ConnectionId}), joined {Count} rooms",
            userId, Context.ConnectionId, rooms.Count);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        _logger.LogInformation("User {UserId} disconnected from ChatHub ({ConnectionId})",
            userId, Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    // ==================== Join / Leave Room ====================

    /// <summary>Join a specific room's SignalR group (for newly opened rooms).</summary>
    public async Task JoinRoom(int roomId)
    {
        var userId = GetUserId();
        var room = await _chatService.GetRoomAsync(roomId, userId);
        if (room is null)
        {
            await SendToCaller("Error", "You do not have access to this room.");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{roomId}");
        _logger.LogInformation("User {UserId} joined room group room:{RoomId}", userId, roomId);
    }

    /// <summary>Leave a specific room's SignalR group.</summary>
    public async Task LeaveRoom(int roomId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"room:{roomId}");
        _logger.LogInformation("User {UserId} left room group room:{RoomId}", GetUserId(), roomId);
    }

    // ==================== Send Message ====================

    /// <summary>Send a text message to a room (no attachments). Persists first, then broadcasts.</summary>
    public async Task SendMessage(int roomId, string? content)
    {
        var userId = GetUserId();
        if (userId == 0) return;

        try
        {
            var result = await _chatService.SendMessageAsync(roomId, userId, content, null);
            if (!result.Success)
            {
                await SendToCaller("Error", result.Message!);
                return;
            }

            var message = await _chatService.GetLatestMessageAsync(roomId, userId);
            if (message is not null && message.SenderId == userId)
            {
                await SendToGroup($"room:{roomId}", "ReceiveMessage", message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendMessage failed. RoomId={RoomId}, UserId={UserId}", roomId, userId);
            await SendToCaller("Error", "Failed to send message. Please try again.");
        }
    }

    /// <summary>Send a message with file attachments. Persists first, then broadcasts.</summary>
    public async Task SendMessageWithAttachments(int roomId, string? content, List<ChatAttachmentInput> attachments)
    {
        var userId = GetUserId();
        if (userId == 0) return;

        try
        {
            var result = await _chatService.SendMessageAsync(roomId, userId, content, attachments);
            if (!result.Success)
            {
                await SendToCaller("Error", result.Message!);
                return;
            }

            var message = await _chatService.GetLatestMessageAsync(roomId, userId);
            if (message is not null && message.SenderId == userId)
            {
                await SendToGroup($"room:{roomId}", "ReceiveMessage", message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendMessageWithAttachments failed. RoomId={RoomId}, UserId={UserId}", roomId, userId);
            await SendToCaller("Error", "Failed to send message. Please try again.");
        }
    }

    // ==================== Edit Message ====================

    /// <summary>Edit an existing message. Broadcasts the updated message to the room.</summary>
    public async Task EditMessage(int roomId, long messageId, string? newContent)
    {
        var userId = GetUserId();
        if (userId == 0) return;

        var result = await _chatService.EditMessageAsync(roomId, messageId, userId, newContent);
        if (!result.Success)
        {
            await SendToCaller("Error", result.Message!);
            return;
        }

        await SendToGroup($"room:{roomId}", "MessageEdited", new
        {
            MessageId = messageId,
            RoomId = roomId,
            Content = newContent,
            EditedAt = DateTime.UtcNow
        });
    }

    // ==================== Delete Message ====================

    /// <summary>Soft-delete a message. Broadcasts deletion to the room.</summary>
    public async Task DeleteMessage(int roomId, long messageId)
    {
        var userId = GetUserId();
        if (userId == 0) return;

        var result = await _chatService.DeleteMessageAsync(roomId, messageId, userId);
        if (!result.Success)
        {
            await SendToCaller("Error", result.Message!);
            return;
        }

        await SendToGroup($"room:{roomId}", "MessageDeleted", new
        {
            MessageId = messageId,
            RoomId = roomId,
            DeletedAt = DateTime.UtcNow
        });
    }

    // ==================== Mark Read ====================

    /// <summary>Mark messages as read up to the given message ID.</summary>
    public async Task MarkRead(int roomId, long lastReadMessageId)
    {
        var userId = GetUserId();
        if (userId == 0) return;

        await _chatService.MarkReadAsync(roomId, userId, lastReadMessageId);
    }

    // ==================== Create Rooms ====================

    /// <summary>Create a group chat room and notify the creator.</summary>
    public async Task CreateGroupRoom(string? roomName, List<int>? memberUserIds)
    {
        var userId = GetUserId();
        if (userId == 0) return;

        var result = await _chatService.CreateGroupRoomAsync(userId, roomName, memberUserIds);
        if (!result.Success)
        {
            await SendToCaller("Error", result.Message!);
            return;
        }

        var room = result.Data!;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{room.RoomId}");
        await SendToCaller("RoomCreated", room);
    }

    /// <summary>Create or get a DM room with another user.</summary>
    public async Task CreateOrGetDmRoom(int otherUserId)
    {
        var userId = GetUserId();
        if (userId == 0) return;

        var result = await _chatService.CreateOrGetDmRoomAsync(userId, otherUserId);
        if (!result.Success)
        {
            await SendToCaller("Error", result.Message!);
            return;
        }

        var room = result.Data!;
        await Groups.AddToGroupAsync(Context.ConnectionId, $"room:{room.RoomId}");
        await SendToCaller("RoomCreated", room);
    }

    // ==================== Helpers ====================

    private int GetUserId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var id) ? id : 0;
    }

    /// <summary>Send a message to the calling client.</summary>
    private Task SendToCaller(string method, object arg)
        => Clients.Caller.SendCoreAsync(method, new[] { arg });

    /// <summary>Send a message to all clients in a group.</summary>
    private Task SendToGroup(string groupName, string method, object arg)
        => Clients.Group(groupName).SendCoreAsync(method, new[] { arg });
}
