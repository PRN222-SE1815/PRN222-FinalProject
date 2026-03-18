using BusinessLogic.DTOs.Request;
using BusinessLogic.DTOs.Response;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Entities;
using BusinessObject.Enum;
using DataAccess.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace BusinessLogic.Services.Implements;

public sealed class StudentScheduleService : IStudentScheduleService
{
    private const string DefaultTimezone = "Asia/Ho_Chi_Minh";

    private static readonly string[] DefaultEnrollmentStatuses = ["ENROLLED"];
    private static readonly HashSet<string> StudentVisibleStatuses =
    ["PUBLISHED", "RESCHEDULED", "COMPLETED"];

    private static readonly string[] ColorPalette =
    [
        "#1D4ED8", "#0EA5E9", "#10B981", "#22C55E", "#84CC16", "#EAB308",
        "#F59E0B", "#F97316", "#EF4444", "#EC4899", "#8B5CF6", "#6366F1"
    ];

    private readonly IScheduleRepository _scheduleRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<StudentScheduleService> _logger;

    public StudentScheduleService(
        IScheduleRepository scheduleRepository,
        IStudentRepository studentRepository,
        IUserRepository userRepository,
        ILogger<StudentScheduleService> logger)
    {
        _scheduleRepository = scheduleRepository;
        _studentRepository = studentRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<ServiceResult<StudentCalendarResponseDto>> GetStudentCalendarAsync(
        int userId,
        GetStudentCalendarRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ServiceResult<StudentCalendarResponseDto>.Fail("INVALID_INPUT", "Request is required.");
        }

        if (userId <= 0)
        {
            return ServiceResult<StudentCalendarResponseDto>.Fail("INVALID_INPUT", "Invalid user id.");
        }

        if (request.FromUtc >= request.ToUtc)
        {
            return ServiceResult<StudentCalendarResponseDto>.Fail("INVALID_INPUT", "FromUtc must be earlier than ToUtc.");
        }

        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null || !string.Equals(user.Role, UserRole.STUDENT.ToString(), StringComparison.OrdinalIgnoreCase) || !user.IsActive)
        {
            return ServiceResult<StudentCalendarResponseDto>.Fail("FORBIDDEN", "User is not allowed to access student calendar.");
        }

        var student = await _studentRepository.GetStudentByUserIdAsync(userId);
        if (student == null || student.StudentId != userId)
        {
            return ServiceResult<StudentCalendarResponseDto>.Fail("FORBIDDEN", "Student profile not found or ownership is invalid.");
        }

        var scheduleEvents = await _scheduleRepository.GetStudentScheduleEventsAsync(
            student.StudentId,
            request.FromUtc,
            request.ToUtc,
            DefaultEnrollmentStatuses,
            cancellationToken);

        var recurrenceIds = scheduleEvents
            .Where(e => e.RecurrenceId.HasValue)
            .Select(e => e.RecurrenceId!.Value)
            .Distinct()
            .ToList();

        var overrides = recurrenceIds.Count == 0
            ? []
            : await _scheduleRepository.GetOverridesByRecurrenceIdsAsync(recurrenceIds, cancellationToken);

        var normalizedOverrides = overrides
            .GroupBy(o => new { o.RecurrenceId, o.OriginalDate })
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.OverrideId).First());

        var filteredItems = new List<CalendarEventDto>(scheduleEvents.Count);

        foreach (var item in scheduleEvents)
        {
            if (!StudentVisibleStatuses.Contains(item.Status))
            {
                continue;
            }

            var effectiveStart = item.StartAt;
            var effectiveEnd = item.EndAt;
            var effectiveLocation = item.Location;

            if (item.RecurrenceId.HasValue)
            {
                var originalDate = DateOnly.FromDateTime(item.StartAt.Date);
                if (normalizedOverrides.TryGetValue(new { RecurrenceId = item.RecurrenceId.Value, OriginalDate = originalDate }, out var ov))
                {
                    if (string.Equals(ov.OverrideType, "CANCEL", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (string.Equals(ov.OverrideType, "RESCHEDULE", StringComparison.OrdinalIgnoreCase))
                    {
                        effectiveStart = ov.NewStartAt ?? effectiveStart;
                        effectiveEnd = ov.NewEndAt ?? effectiveEnd;
                        effectiveLocation = string.IsNullOrWhiteSpace(ov.NewLocation) ? effectiveLocation : ov.NewLocation;
                    }
                }
            }

            if (!IsOverlap(effectiveStart, effectiveEnd, request.FromUtc, request.ToUtc))
            {
                continue;
            }

            filteredItems.Add(MapCalendarEvent(item, effectiveStart, effectiveEnd, effectiveLocation));
        }

        var ordered = filteredItems
            .OrderBy(e => e.StartAtUtc)
            .ThenBy(e => e.ScheduleEventId)
            .ToList();

        var response = new StudentCalendarResponseDto
        {
            FromUtc = request.FromUtc,
            ToUtc = request.ToUtc,
            Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? DefaultTimezone : request.Timezone.Trim(),
            Events = ordered
        };

        _logger.LogInformation("GetStudentCalendarAsync success. UserId={UserId}, TotalEvents={TotalEvents}", userId, ordered.Count);
        return ServiceResult<StudentCalendarResponseDto>.Success(response, "Student calendar loaded successfully.");
    }

    private static bool IsOverlap(DateTime eventStart, DateTime eventEnd, DateTime fromUtc, DateTime toUtc)
    {
        return eventEnd > fromUtc && eventStart < toUtc;
    }

    private static CalendarEventDto MapCalendarEvent(
        ScheduleEvent scheduleEvent,
        DateTime startAtUtc,
        DateTime endAtUtc,
        string? location)
    {
        var classSection = scheduleEvent.ClassSection;
        var course = classSection.Course;
        var semester = classSection.Semester;

        return new CalendarEventDto
        {
            ScheduleEventId = scheduleEvent.ScheduleEventId,
            Title = scheduleEvent.Title,
            StartAtUtc = startAtUtc,
            EndAtUtc = endAtUtc,
            Timezone = string.IsNullOrWhiteSpace(scheduleEvent.Timezone) ? DefaultTimezone : scheduleEvent.Timezone,
            Location = location,
            OnlineUrl = scheduleEvent.OnlineUrl,
            ClassSectionId = scheduleEvent.ClassSectionId,
            SectionCode = classSection.SectionCode,
            CourseCode = course.CourseCode,
            CourseName = course.CourseName,
            SemesterCode = semester.SemesterCode,
            TeacherName = ResolveTeacherName(scheduleEvent),
            Status = (scheduleEvent.Status ?? string.Empty).ToUpperInvariant(),
            Color = ResolveColor(course.CourseCode, classSection.ClassSectionId),
            IsRecurring = scheduleEvent.RecurrenceId.HasValue,
            RecurrenceId = scheduleEvent.RecurrenceId
        };
    }

    private static string ResolveTeacherName(ScheduleEvent scheduleEvent)
    {
        return scheduleEvent.Teacher?.TeacherNavigation?.FullName
            ?? scheduleEvent.ClassSection?.Teacher?.TeacherNavigation?.FullName
            ?? string.Empty;
    }

    private static string ResolveColor(string? courseCode, int classSectionId)
    {
        var seed = $"{courseCode?.Trim().ToUpperInvariant()}::{classSectionId}";
        var hash = 17;

        foreach (var c in seed)
        {
            hash = (hash * 31) + c;
        }

        if (hash < 0)
        {
            hash *= -1;
        }

        var index = hash % ColorPalette.Length;
        return ColorPalette[index];
    }
}
