using BusinessLogic.DTOs.Request;
using BusinessLogic.DTOs.Response;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Entities;
using BusinessObject.Enum;
using DataAccess.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace BusinessLogic.Services.Implements;

public sealed class AdminScheduleService : IAdminScheduleService
{
    private static readonly HashSet<string> ValidStatuses =
    ["DRAFT", "PUBLISHED", "RESCHEDULED", "CANCELLED", "COMPLETED", "ARCHIVED"];

    private readonly IScheduleRepository _scheduleRepository;
    private readonly IUserRepository _userRepository;
    private readonly INotificationService _notificationService;
    private readonly ILogger<AdminScheduleService> _logger;

    public AdminScheduleService(
        IScheduleRepository scheduleRepository,
        IUserRepository userRepository,
        INotificationService notificationService,
        ILogger<AdminScheduleService> logger)
    {
        _scheduleRepository = scheduleRepository;
        _userRepository = userRepository;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<ServiceResult<AdminSchedulePageDto>> GetSchedulesAsync(
        int adminUserId,
        AdminScheduleFilterRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ServiceResult<AdminSchedulePageDto>.Fail("INVALID_INPUT", "Request is required.");
        }

        var adminValidation = await ValidateAdminAsync(adminUserId);
        if (!adminValidation.IsSuccess)
        {
            return ServiceResult<AdminSchedulePageDto>.Fail(adminValidation.ErrorCode!, adminValidation.Message!);
        }

        var page = request.Page <= 0 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? 20 : request.PageSize;

        var status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status.Trim().ToUpperInvariant();
        if (status is not null && !ValidStatuses.Contains(status))
        {
            return ServiceResult<AdminSchedulePageDto>.Fail("INVALID_STATUS", "Invalid schedule status filter.");
        }

        var (items, totalCount) = await _scheduleRepository.GetAdminScheduleEventsAsync(
            page,
            pageSize,
            request.FromUtc,
            request.ToUtc,
            request.SemesterId,
            request.ClassSectionId,
            request.TeacherId,
            status,
            cancellationToken);

        var mappedItems = items
            .Select(MapAdminItem)
            .ToList();

        var result = new AdminSchedulePageDto
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = mappedItems
        };

        return ServiceResult<AdminSchedulePageDto>.Success(result, "Schedules loaded successfully.");
    }

    public async Task<ServiceResult<AdminScheduleItemDto>> GetScheduleDetailAsync(
        int adminUserId,
        long scheduleEventId,
        CancellationToken cancellationToken = default)
    {
        var adminValidation = await ValidateAdminAsync(adminUserId);
        if (!adminValidation.IsSuccess)
        {
            return ServiceResult<AdminScheduleItemDto>.Fail(adminValidation.ErrorCode!, adminValidation.Message!);
        }

        if (scheduleEventId <= 0)
        {
            return ServiceResult<AdminScheduleItemDto>.Fail("INVALID_INPUT", "Invalid scheduleEventId.");
        }

        var scheduleEvent = await _scheduleRepository.GetScheduleEventDetailAsync(scheduleEventId, cancellationToken);
        if (scheduleEvent is null)
        {
            return ServiceResult<AdminScheduleItemDto>.Fail("SCHEDULE_NOT_FOUND", "Schedule event not found.");
        }

        return ServiceResult<AdminScheduleItemDto>.Success(MapAdminItem(scheduleEvent), "Schedule detail loaded successfully.");
    }

    public async Task<ServiceResult<AdminScheduleItemDto>> CreateScheduleEventAsync(
        int adminUserId,
        CreateScheduleEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminValidation = await ValidateAdminAsync(adminUserId);
        if (!adminValidation.IsSuccess)
        {
            return ServiceResult<AdminScheduleItemDto>.Fail(adminValidation.ErrorCode!, adminValidation.Message!);
        }

        if (request is null)
        {
            return ServiceResult<AdminScheduleItemDto>.Fail("INVALID_INPUT", "Request is required.");
        }

        if (request.ClassSectionId <= 0 || string.IsNullOrWhiteSpace(request.Title) || request.EndAtUtc <= request.StartAtUtc)
        {
            return ServiceResult<AdminScheduleItemDto>.Fail("INVALID_INPUT", "Invalid create schedule payload.");
        }

        var status = string.IsNullOrWhiteSpace(request.InitialStatus)
            ? "DRAFT"
            : request.InitialStatus.Trim().ToUpperInvariant();

        if (!ValidStatuses.Contains(status))
        {
            return ServiceResult<AdminScheduleItemDto>.Fail("INVALID_STATUS", "Invalid initial status.");
        }

        var now = DateTime.UtcNow;
        var created = await _scheduleRepository.CreateScheduleEventAsync(
            new ScheduleEvent
            {
                ClassSectionId = request.ClassSectionId,
                Title = request.Title.Trim(),
                StartAt = request.StartAtUtc,
                EndAt = request.EndAtUtc,
                Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "Asia/Ho_Chi_Minh" : request.Timezone.Trim(),
                Location = request.Location,
                OnlineUrl = request.OnlineUrl,
                TeacherId = request.TeacherId,
                Status = status,
                RecurrenceId = request.RecurrenceId,
                CreatedBy = adminUserId,
                CreatedAt = now,
                UpdatedBy = adminUserId,
                UpdatedAt = now
            },
            new ScheduleChangeLog
            {
                ActorUserId = adminUserId,
                ChangeType = "CREATE",
                Reason = request.Reason,
                NewJson = JsonSerializer.Serialize(new
                {
                    request.ClassSectionId,
                    request.Title,
                    request.StartAtUtc,
                    request.EndAtUtc,
                    Status = status
                }),
                CreatedAt = now
            },
            cancellationToken);

        var createdItem = await _scheduleRepository.GetScheduleEventDetailAsync(created.ScheduleEventId, cancellationToken);
        if (createdItem is null)
        {
            return ServiceResult<AdminScheduleItemDto>.Fail("INTERNAL_ERROR", "Schedule event created but cannot be loaded.");
        }

        await NotifyScheduleChangedAsync(adminUserId, createdItem, "CREATE", cancellationToken);
        return ServiceResult<AdminScheduleItemDto>.Success(MapAdminItem(createdItem), "Schedule event created successfully.");
    }

    public async Task<ServiceResult<AdminScheduleItemDto>> UpdateScheduleEventAsync(
        int adminUserId,
        UpdateScheduleEventRequest request,
        CancellationToken cancellationToken = default)
    {
        var adminValidation = await ValidateAdminAsync(adminUserId);
        if (!adminValidation.IsSuccess)
        {
            return ServiceResult<AdminScheduleItemDto>.Fail(adminValidation.ErrorCode!, adminValidation.Message!);
        }

        if (request is null)
        {
            return ServiceResult<AdminScheduleItemDto>.Fail("INVALID_INPUT", "Request is required.");
        }

        if (request.ScheduleEventId <= 0 || string.IsNullOrWhiteSpace(request.Title) || request.EndAtUtc <= request.StartAtUtc)
        {
            return ServiceResult<AdminScheduleItemDto>.Fail("INVALID_INPUT", "Invalid update schedule payload.");
        }

        var existing = await _scheduleRepository.GetScheduleEventDetailAsync(request.ScheduleEventId, cancellationToken);
        if (existing is null)
        {
            return ServiceResult<AdminScheduleItemDto>.Fail("SCHEDULE_NOT_FOUND", "Schedule event not found.");
        }

        var updated = await _scheduleRepository.UpdateScheduleEventAsync(
            new ScheduleEvent
            {
                ScheduleEventId = request.ScheduleEventId,
                Title = request.Title.Trim(),
                StartAt = request.StartAtUtc,
                EndAt = request.EndAtUtc,
                Timezone = string.IsNullOrWhiteSpace(request.Timezone)
                    ? existing.Timezone
                    : request.Timezone.Trim(),
                Location = request.Location,
                OnlineUrl = request.OnlineUrl,
                TeacherId = request.TeacherId,
                RecurrenceId = request.RecurrenceId,
                UpdatedBy = adminUserId,
                UpdatedAt = DateTime.UtcNow
            },
            new ScheduleChangeLog
            {
                ScheduleEventId = request.ScheduleEventId,
                ActorUserId = adminUserId,
                ChangeType = "UPDATE",
                OldJson = JsonSerializer.Serialize(new
                {
                    existing.Title,
                    StartAtUtc = existing.StartAt,
                    EndAtUtc = existing.EndAt,
                    existing.Status
                }),
                NewJson = JsonSerializer.Serialize(new
                {
                    request.Title,
                    request.StartAtUtc,
                    request.EndAtUtc,
                    existing.Status
                }),
                Reason = request.Reason,
                CreatedAt = DateTime.UtcNow
            },
            cancellationToken);

        if (updated is null)
        {
            return ServiceResult<AdminScheduleItemDto>.Fail("NOT_FOUND", "Schedule event not found.");
        }

        var detail = await _scheduleRepository.GetScheduleEventDetailAsync(request.ScheduleEventId, cancellationToken);
        if (detail is null)
        {
            return ServiceResult<AdminScheduleItemDto>.Fail("INTERNAL_ERROR", "Schedule event updated but cannot be loaded.");
        }

        await NotifyScheduleChangedAsync(adminUserId, detail, "UPDATE", cancellationToken);
        return ServiceResult<AdminScheduleItemDto>.Success(MapAdminItem(detail), "Schedule event updated successfully.");
    }

    public async Task<ServiceResult<bool>> ChangeScheduleStatusAsync(
        int adminUserId,
        long scheduleEventId,
        string newStatus,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var adminValidation = await ValidateAdminAsync(adminUserId);
        if (!adminValidation.IsSuccess)
        {
            return ServiceResult<bool>.Fail(adminValidation.ErrorCode!, adminValidation.Message!);
        }

        if (scheduleEventId <= 0 || string.IsNullOrWhiteSpace(newStatus))
        {
            return ServiceResult<bool>.Fail("INVALID_INPUT", "Invalid scheduleEventId or status.");
        }

        var normalizedStatus = newStatus.Trim().ToUpperInvariant();
        if (!ValidStatuses.Contains(normalizedStatus))
        {
            return ServiceResult<bool>.Fail("INVALID_STATUS", "Invalid status.");
        }

        var existing = await _scheduleRepository.GetScheduleEventDetailAsync(scheduleEventId, cancellationToken);
        if (existing is null)
        {
            return ServiceResult<bool>.Fail("SCHEDULE_NOT_FOUND", "Schedule event not found.");
        }

        var currentStatus = (existing.Status ?? string.Empty).ToUpperInvariant();

        if (string.Equals(currentStatus, "ARCHIVED", StringComparison.Ordinal) && string.Equals(normalizedStatus, "DRAFT", StringComparison.Ordinal))
        {
            return ServiceResult<bool>.Fail("INVALID_STATUS_TRANSITION", "Archived schedule cannot transition back to DRAFT.");
        }

        if (string.Equals(currentStatus, "CANCELLED", StringComparison.Ordinal) && string.Equals(normalizedStatus, "PUBLISHED", StringComparison.Ordinal))
        {
            return ServiceResult<bool>.Fail("INVALID_STATUS_TRANSITION", "Cancelled schedule cannot transition back to PUBLISHED.");
        }

        var changed = await _scheduleRepository.ChangeScheduleStatusAsync(
            scheduleEventId,
            normalizedStatus,
            adminUserId,
            reason,
            cancellationToken);

        if (!changed)
        {
            return ServiceResult<bool>.Fail("NOT_FOUND", "Schedule event not found.");
        }

        var detail = await _scheduleRepository.GetScheduleEventDetailAsync(scheduleEventId, cancellationToken);
        if (detail is not null)
        {
            var changeType = normalizedStatus switch
            {
                "CANCELLED" => "CANCEL",
                "PUBLISHED" => "PUBLISH",
                _ => "UPDATE"
            };

            await NotifyScheduleChangedAsync(adminUserId, detail, changeType, cancellationToken);
        }

        return ServiceResult<bool>.Success(true);
    }

    private async Task NotifyScheduleChangedAsync(
        int actorUserId,
        ScheduleEvent scheduleEvent,
        string changeType,
        CancellationToken cancellationToken)
    {
        var teacherUserId = scheduleEvent.TeacherId ?? scheduleEvent.ClassSection?.TeacherId;
        var studentUserIds = await _scheduleRepository
            .GetEnrolledStudentUserIdsByClassSectionAsync(scheduleEvent.ClassSectionId, cancellationToken);

        if (teacherUserId.HasValue && teacherUserId.Value > 0)
        {
            await _notificationService.CreateScheduleNotificationAsync(actorUserId, new CreateScheduleNotificationRequest
            {
                ScheduleEventId = scheduleEvent.ScheduleEventId,
                ChangeType = changeType,
                Title = scheduleEvent.Title,
                Message = BuildScheduleNotificationMessage(scheduleEvent, changeType),
                RecipientUserIds = [teacherUserId.Value],
                Payload = new
                {
                    deepLink = "/Teacher/TeacherSchedule/Index",
                    scheduleEventId = scheduleEvent.ScheduleEventId,
                    classSectionId = scheduleEvent.ClassSectionId,
                    changeType
                }
            }, cancellationToken);
        }

        if (studentUserIds.Count > 0)
        {
            await _notificationService.CreateScheduleNotificationAsync(actorUserId, new CreateScheduleNotificationRequest
            {
                ScheduleEventId = scheduleEvent.ScheduleEventId,
                ChangeType = changeType,
                Title = scheduleEvent.Title,
                Message = BuildScheduleNotificationMessage(scheduleEvent, changeType),
                RecipientUserIds = studentUserIds,
                Payload = new
                {
                    deepLink = "/Student/StudentSchedule/Index",
                    scheduleEventId = scheduleEvent.ScheduleEventId,
                    classSectionId = scheduleEvent.ClassSectionId,
                    changeType
                }
            }, cancellationToken);
        }
    }

    private static string BuildScheduleNotificationMessage(ScheduleEvent scheduleEvent, string changeType)
    {
        var action = changeType.ToUpperInvariant() switch
        {
            "CREATE" => "created",
            "UPDATE" => "updated",
            "CANCEL" => "cancelled",
            "PUBLISH" => "published",
            _ => "changed"
        };

        return $"Schedule event \"{scheduleEvent.Title}\" has been {action}.";
    }

    private async Task<ServiceResult<bool>> ValidateAdminAsync(int adminUserId)
    {
        if (adminUserId <= 0)
        {
            return ServiceResult<bool>.Fail("INVALID_INPUT", "Invalid admin user id.");
        }

        var user = await _userRepository.GetUserByIdAsync(adminUserId);
        if (user is null || !user.IsActive || !string.Equals(user.Role, UserRole.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<bool>.Fail("FORBIDDEN", "User is not allowed to manage schedules.");
        }

        return ServiceResult<bool>.Success(true);
    }

    private static AdminScheduleItemDto MapAdminItem(ScheduleEvent scheduleEvent)
    {
        return new AdminScheduleItemDto
        {
            ScheduleEventId = scheduleEvent.ScheduleEventId,
            Title = scheduleEvent.Title,
            StartAtUtc = scheduleEvent.StartAt,
            EndAtUtc = scheduleEvent.EndAt,
            Status = (scheduleEvent.Status ?? string.Empty).ToUpperInvariant(),
            ClassSectionId = scheduleEvent.ClassSectionId,
            SectionCode = scheduleEvent.ClassSection?.SectionCode ?? string.Empty,
            CourseCode = scheduleEvent.ClassSection?.Course?.CourseCode ?? string.Empty,
            CourseName = scheduleEvent.ClassSection?.Course?.CourseName ?? string.Empty,
            SemesterCode = scheduleEvent.ClassSection?.Semester?.SemesterCode ?? string.Empty,
            TeacherName = ResolveTeacherName(scheduleEvent),
            CreatedAtUtc = scheduleEvent.CreatedAt,
            UpdatedAtUtc = scheduleEvent.UpdatedAt
        };
    }

    private static string ResolveTeacherName(ScheduleEvent scheduleEvent)
    {
        return scheduleEvent.Teacher?.TeacherNavigation?.FullName
            ?? scheduleEvent.ClassSection?.Teacher?.TeacherNavigation?.FullName
            ?? string.Empty;
    }
}
