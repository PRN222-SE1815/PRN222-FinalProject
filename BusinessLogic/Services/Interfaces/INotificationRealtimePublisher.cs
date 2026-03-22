using BusinessLogic.DTOs.Responses;

namespace BusinessLogic.Services.Interfaces;

public interface INotificationRealtimePublisher
{
    Task PublishToUsersAsync(IReadOnlyCollection<int> userIds, NotificationItemDto notification, CancellationToken ct = default);
}
