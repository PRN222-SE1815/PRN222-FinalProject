using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Presentation.Hubs;

[Authorize]
public sealed class NotificationHub : Hub
{
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(ILogger<NotificationHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = GetUserId();
        if (userId <= 0)
        {
            _logger.LogWarning("NotificationHub connection rejected - invalid user claim. ConnectionId={ConnectionId}", Context.ConnectionId);
            Context.Abort();
            return;
        }

        var correlationId = Context.GetHttpContext()?.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? Context.ConnectionId;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");
        _logger.LogInformation(
            "NotificationHub connected. UserId={UserId}, ConnectionId={ConnectionId}, CorrelationId={CorrelationId}",
            userId, Context.ConnectionId, correlationId);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = GetUserId();
        if (userId > 0)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{userId}");
        }

        if (exception is not null)
        {
            _logger.LogWarning(exception,
                "NotificationHub disconnected with error. UserId={UserId}, ConnectionId={ConnectionId}",
                userId, Context.ConnectionId);
        }
        else
        {
            _logger.LogInformation(
                "NotificationHub disconnected. UserId={UserId}, ConnectionId={ConnectionId}",
                userId, Context.ConnectionId);
        }

        await base.OnDisconnectedAsync(exception);
    }

    private int GetUserId()
    {
        var claim = Context.User?.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var userId) ? userId : 0;
    }
}
