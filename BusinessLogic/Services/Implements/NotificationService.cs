using System.Text.Json;
using BusinessLogic.DTOs.Request;
using BusinessLogic.DTOs.Response;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Entities;
using BusinessLogic.Settings;
using BusinessObject.Enum;
using DataAccess.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BusinessLogic.Services.Implements;

public sealed class NotificationService : INotificationService
{
    private const int MaxPageSize = 100;

    private readonly INotificationRepository _notificationRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationRealtimePublisher _realtimePublisher;
    private readonly ReliabilitySettings _reliability;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        INotificationRealtimePublisher realtimePublisher,
        IOptions<ReliabilitySettings> reliabilityOptions,
        ILogger<NotificationService> logger)
    {
        _notificationRepository = notificationRepository;
        _userRepository = userRepository;
        _realtimePublisher = realtimePublisher;
        _reliability = reliabilityOptions.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<bool>> SendAsync(
        string notificationType,
        string title,
        string message,
        string? deepLink,
        IReadOnlyCollection<int> recipientUserIds,
        CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(notificationType) || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(message))
            {
                return ServiceResult<bool>.Fail("INVALID_INPUT", "Notification type, title, and message are required.");
            }

            var distinctRecipients = (recipientUserIds ?? Array.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (distinctRecipients.Count == 0)
            {
                return ServiceResult<bool>.Fail("INVALID_INPUT", "Recipient list cannot be empty.");
            }

            var payload = new GenericNotificationPayload
            {
                Title = title.Trim(),
                Message = message.Trim(),
                DeepLink = deepLink
            };

            var notification = new Notification
            {
                NotificationType = notificationType.Trim(),
                PayloadJson = JsonSerializer.Serialize(payload),
                Status = NotificationStatus.SENT.ToString(),
                CreatedAt = DateTime.UtcNow,
                SentAt = DateTime.UtcNow
            };

            var created = await _notificationRepository.CreateNotificationAsync(notification, distinctRecipients, ct);

            var item = new NotificationItemDto
            {
                NotificationId = created.NotificationId,
                NotificationType = created.NotificationType,
                Title = payload.Title,
                Message = payload.Message,
                CreatedAtUtc = created.CreatedAt,
                IsRead = false,
                DeepLink = payload.DeepLink
            };

            await PublishWithRetryAsync(distinctRecipients, item, ct);

            _logger.LogInformation(
                "Notification sent. Type={NotificationType}, NotificationId={NotificationId}, Recipients={RecipientCount}",
                notificationType,
                created.NotificationId,
                distinctRecipients.Count);

            return ServiceResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SendAsync failed. NotificationType={NotificationType}", notificationType);
            return ServiceResult<bool>.Fail("INTERNAL_ERROR", "Unable to send notification.");
        }
    }

    public async Task<ServiceResult<bool>> CreateScheduleNotificationAsync(
        int actorUserId,
        CreateScheduleNotificationRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (request is null || request.ScheduleEventId <= 0 || string.IsNullOrWhiteSpace(request.ChangeType)
                || string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Message))
            {
                return ServiceResult<bool>.Fail("INVALID_INPUT", "Invalid schedule notification payload.");
            }

            var actor = await _userRepository.GetUserByIdAsync(actorUserId);
            if (actor is null || !actor.IsActive)
            {
                _logger.LogWarning("CreateScheduleNotificationAsync denied because actor invalid. ActorUserId={ActorUserId}", actorUserId);
                return ServiceResult<bool>.Fail("FORBIDDEN", "Actor is invalid.");
            }

            var isAllowedRole = string.Equals(actor.Role, UserRole.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(actor.Role, UserRole.TEACHER.ToString(), StringComparison.OrdinalIgnoreCase);

            if (!isAllowedRole)
            {
                return ServiceResult<bool>.Fail("FORBIDDEN", "User is not allowed to create schedule notifications.");
            }

            var recipientUserIds = (request.RecipientUserIds ?? Array.Empty<int>())
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            if (recipientUserIds.Count == 0)
            {
                _logger.LogWarning(
                    "CreateScheduleNotificationAsync ignored because recipient list is empty. ActorUserId={ActorUserId}, ScheduleEventId={ScheduleEventId}",
                    actorUserId,
                    request.ScheduleEventId);
                return ServiceResult<bool>.Fail("INVALID_INPUT", "Recipient list cannot be empty.");
            }

            var payload = BuildPayload(request);

            var notification = new Notification
            {
                NotificationType = "SCHEDULE_CHANGED",
                PayloadJson = JsonSerializer.Serialize(payload),
                Status = NotificationStatus.SENT.ToString(),
                CreatedAt = DateTime.UtcNow,
                SentAt = DateTime.UtcNow
            };

            var created = await _notificationRepository.CreateNotificationAsync(notification, recipientUserIds, ct);

            var item = new NotificationItemDto
            {
                NotificationId = created.NotificationId,
                NotificationType = created.NotificationType,
                Title = payload.Title,
                Message = payload.Message,
                CreatedAtUtc = created.CreatedAt,
                IsRead = false,
                DeepLink = payload.DeepLink
            };

            await PublishWithRetryAsync(recipientUserIds, item, ct);

            _logger.LogInformation(
                "Schedule notification created and pushed. NotificationId={NotificationId}, ScheduleEventId={ScheduleEventId}, Recipients={RecipientCount}",
                created.NotificationId,
                request.ScheduleEventId,
                recipientUserIds.Count);

            return ServiceResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateScheduleNotificationAsync failed. ActorUserId={ActorUserId}", actorUserId);
            return ServiceResult<bool>.Fail("INTERNAL_ERROR", "Unable to create schedule notification.");
        }
    }

    public async Task<ServiceResult<PagedResultDto<NotificationItemDto>>> GetMyNotificationsAsync(
        int userId,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default)
    {
        try
        {
            if (userId <= 0)
            {
                return ServiceResult<PagedResultDto<NotificationItemDto>>.Fail("INVALID_INPUT", "Invalid user id.");
            }

            page = page <= 0 ? 1 : page;
            pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, MaxPageSize);

            var (items, totalCount) = await _notificationRepository.GetUserNotificationsAsync(userId, page, pageSize, ct);
            var mappedItems = items.Select(MapItem).ToList();

            var result = new PagedResultDto<NotificationItemDto>
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Items = mappedItems
            };

            return ServiceResult<PagedResultDto<NotificationItemDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMyNotificationsAsync failed. UserId={UserId}", userId);
            return ServiceResult<PagedResultDto<NotificationItemDto>>.Fail("INTERNAL_ERROR", "Unable to load notifications.");
        }
    }

    public async Task<ServiceResult<int>> GetMyUnreadCountAsync(int userId, CancellationToken ct = default)
    {
        try
        {
            if (userId <= 0)
            {
                return ServiceResult<int>.Fail("INVALID_INPUT", "Invalid user id.");
            }

            var unreadCount = await _notificationRepository.GetUnreadCountAsync(userId, ct);
            return ServiceResult<int>.Success(unreadCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMyUnreadCountAsync failed. UserId={UserId}", userId);
            return ServiceResult<int>.Fail("INTERNAL_ERROR", "Unable to get unread notification count.");
        }
    }

    public async Task<ServiceResult<bool>> MarkAsReadAsync(int userId, long notificationId, CancellationToken ct = default)
    {
        try
        {
            if (userId <= 0 || notificationId <= 0)
            {
                return ServiceResult<bool>.Fail("INVALID_INPUT", "Invalid user id or notification id.");
            }

            var exists = await _notificationRepository.ExistsForUserAsync(notificationId, userId, ct);
            if (!exists)
            {
                return ServiceResult<bool>.Fail("NOT_FOUND", "Notification not found.");
            }

            await _notificationRepository.MarkAsReadAsync(notificationId, userId, ct);
            return ServiceResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MarkAsReadAsync failed. UserId={UserId}, NotificationId={NotificationId}", userId, notificationId);
            return ServiceResult<bool>.Fail("INTERNAL_ERROR", "Unable to mark notification as read.");
        }
    }

    public async Task<ServiceResult<bool>> MarkAllAsReadAsync(int userId, CancellationToken ct = default)
    {
        try
        {
            if (userId <= 0)
            {
                return ServiceResult<bool>.Fail("INVALID_INPUT", "Invalid user id.");
            }

            await _notificationRepository.MarkAllAsReadAsync(userId, ct);
            return ServiceResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MarkAllAsReadAsync failed. UserId={UserId}", userId);
            return ServiceResult<bool>.Fail("INTERNAL_ERROR", "Unable to mark all notifications as read.");
        }
    }

    private static NotificationItemDto MapItem(NotificationRecipient recipient)
    {
        var payload = DeserializePayload(recipient.Notification.PayloadJson);

        return new NotificationItemDto
        {
            NotificationId = recipient.NotificationId,
            NotificationType = recipient.Notification.NotificationType,
            Title = payload.Title,
            Message = payload.Message,
            CreatedAtUtc = recipient.Notification.CreatedAt,
            IsRead = recipient.ReadAt.HasValue,
            DeepLink = payload.DeepLink
        };
    }

    private static ScheduleNotificationPayload BuildPayload(CreateScheduleNotificationRequest request)
    {
        var currentPayload = request.Payload;
        string? deepLink = null;

        if (currentPayload is JsonElement jsonElement && jsonElement.TryGetProperty("deepLink", out var deepLinkElement))
        {
            deepLink = deepLinkElement.GetString();
        }

        return new ScheduleNotificationPayload
        {
            ScheduleEventId = request.ScheduleEventId,
            ChangeType = request.ChangeType.Trim().ToUpperInvariant(),
            Title = request.Title.Trim(),
            Message = request.Message.Trim(),
            DeepLink = string.IsNullOrWhiteSpace(deepLink) ? null : deepLink,
            Payload = currentPayload
        };
    }

    private static ScheduleNotificationPayload DeserializePayload(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            return new ScheduleNotificationPayload();
        }

        try
        {
            var payload = JsonSerializer.Deserialize<ScheduleNotificationPayload>(payloadJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            return payload ?? new ScheduleNotificationPayload();
        }
        catch
        {
            return new ScheduleNotificationPayload();
        }
    }

    private sealed class ScheduleNotificationPayload
    {
        public long ScheduleEventId { get; set; }
        public string ChangeType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? DeepLink { get; set; }
        public object? Payload { get; set; }
    }

    private sealed class GenericNotificationPayload
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? DeepLink { get; set; }
    }

    private async Task PublishWithRetryAsync(
        IReadOnlyCollection<int> userIds,
        NotificationItemDto item,
        CancellationToken ct)
    {
        var maxRetries = Math.Max(1, _reliability.NotificationRetryCount);
        var baseDelay = Math.Max(50, _reliability.NotificationRetryBaseDelayMs);

        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                await _realtimePublisher.PublishToUsersAsync(userIds, item, ct);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries - 1)
            {
                var delay = baseDelay * (int)Math.Pow(2, attempt);
                _logger.LogWarning(ex,
                    "Realtime push failed, retry {Attempt}/{MaxRetries} in {Delay}ms. NotificationId={NotificationId}",
                    attempt + 1, maxRetries, delay, item.NotificationId);
                await Task.Delay(delay, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Realtime push failed after {MaxRetries} retries. NotificationId={NotificationId}. DB notification saved successfully.",
                    maxRetries, item.NotificationId);
            }
        }
    }
}
