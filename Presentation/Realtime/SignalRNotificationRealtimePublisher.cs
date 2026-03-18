using BusinessLogic.DTOs.Response;
using BusinessLogic.Services.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Presentation.Hubs;

namespace Presentation.Realtime;

public sealed class SignalRNotificationRealtimePublisher : INotificationRealtimePublisher
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<SignalRNotificationRealtimePublisher> _logger;

    public SignalRNotificationRealtimePublisher(
        IHubContext<NotificationHub> hubContext,
        ILogger<SignalRNotificationRealtimePublisher> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task PublishToUsersAsync(IReadOnlyCollection<int> userIds, NotificationItemDto notification, CancellationToken ct = default)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        var targets = userIds.Distinct().ToList();
        foreach (var userId in targets)
        {
            await _hubContext.Clients.Group($"user:{userId}").SendAsync("ReceiveNotification", notification, ct);
        }

        _logger.LogInformation(
            "Realtime notification pushed. NotificationId={NotificationId}, Recipients={RecipientCount}",
            notification.NotificationId,
            targets.Count);
    }
}
