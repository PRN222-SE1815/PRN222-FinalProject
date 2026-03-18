using BusinessLogic.DTOs.Request;
using BusinessLogic.DTOs.Response;

namespace BusinessLogic.Services.Interfaces;

public interface INotificationService
{
    Task<ServiceResult<bool>> SendAsync(
        string notificationType,
        string title,
        string message,
        string? deepLink,
        IReadOnlyCollection<int> recipientUserIds,
        CancellationToken ct = default);

    Task<ServiceResult<bool>> CreateScheduleNotificationAsync(
        int actorUserId,
        CreateScheduleNotificationRequest request,
        CancellationToken ct = default);

    Task<ServiceResult<PagedResultDto<NotificationItemDto>>> GetMyNotificationsAsync(
        int userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);

    Task<ServiceResult<int>> GetMyUnreadCountAsync(int userId, CancellationToken ct = default);

    Task<ServiceResult<bool>> MarkAsReadAsync(int userId, long notificationId, CancellationToken ct = default);

    Task<ServiceResult<bool>> MarkAllAsReadAsync(int userId, CancellationToken ct = default);
}
