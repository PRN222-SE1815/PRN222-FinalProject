using BusinessObject.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface INotificationRepository
{
    Task<Notification> CreateNotificationAsync(
        Notification notification,
        IReadOnlyCollection<int> recipientUserIds,
        CancellationToken ct = default);

    Task<(IReadOnlyList<NotificationRecipient> Items, int TotalCount)> GetUserNotificationsAsync(
        int userId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default);

    Task<bool> ExistsForUserAsync(long notificationId, int userId, CancellationToken ct = default);

    Task MarkAsReadAsync(long notificationId, int userId, CancellationToken ct = default);

    Task MarkAllAsReadAsync(int userId, CancellationToken ct = default);
}
