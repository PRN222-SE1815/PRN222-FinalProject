using BusinessLogic.DTOs.Request;
using BusinessLogic.DTOs.Response;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Entities;
using BusinessObject.Enum;
using DataAccess.Repositories.Interfaces;
using Microsoft.Extensions.Logging;

namespace BusinessLogic.Services.Implements;

public sealed class TeacherScheduleService : ITeacherScheduleService
{
    private const string DefaultTimezone = "Asia/Ho_Chi_Minh";

    private static readonly string[] ColorPalette =
    [
        "#1D4ED8", "#0EA5E9", "#10B981", "#22C55E", "#84CC16", "#EAB308",
        "#F59E0B", "#F97316", "#EF4444", "#EC4899", "#8B5CF6", "#6366F1"
    ];

    private readonly IScheduleRepository _scheduleRepository;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<TeacherScheduleService> _logger;

    public TeacherScheduleService(
        IScheduleRepository scheduleRepository,
        IUserRepository userRepository,
        ILogger<TeacherScheduleService> logger)
    {
        _scheduleRepository = scheduleRepository;
        _userRepository = userRepository;
        _logger = logger;
    }

    public async Task<ServiceResult<StudentCalendarResponseDto>> GetTeacherCalendarAsync(
        int userId,
        GetStudentCalendarRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            return ServiceResult<StudentCalendarResponseDto>.Fail("INVALID_INPUT", "Request is required.");
        }

        if (userId <= 0 || request.FromUtc >= request.ToUtc)
        {
            return ServiceResult<StudentCalendarResponseDto>.Fail("INVALID_INPUT", "Invalid user id or time range.");
        }

        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null || !user.IsActive || !string.Equals(user.Role, UserRole.TEACHER.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<StudentCalendarResponseDto>.Fail("FORBIDDEN", "User is not allowed to access teacher calendar.");
        }

        var events = await _scheduleRepository.GetTeacherScheduleEventsAsync(
            userId,
            request.FromUtc,
            request.ToUtc,
            cancellationToken);

        var mapped = events
            .Select(MapCalendarEvent)
            .OrderBy(x => x.StartAtUtc)
            .ThenBy(x => x.ScheduleEventId)
            .ToList();

        var response = new StudentCalendarResponseDto
        {
            FromUtc = request.FromUtc,
            ToUtc = request.ToUtc,
            Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? DefaultTimezone : request.Timezone.Trim(),
            Events = mapped
        };

        _logger.LogInformation("GetTeacherCalendarAsync success. UserId={UserId}, TotalEvents={TotalEvents}", userId, mapped.Count);
        return ServiceResult<StudentCalendarResponseDto>.Success(response, "Teacher calendar loaded successfully.");
    }

    private static CalendarEventDto MapCalendarEvent(ScheduleEvent scheduleEvent)
    {
        var classSection = scheduleEvent.ClassSection;
        var course = classSection.Course;
        var semester = classSection.Semester;

        return new CalendarEventDto
        {
            ScheduleEventId = scheduleEvent.ScheduleEventId,
            Title = scheduleEvent.Title,
            StartAtUtc = scheduleEvent.StartAt,
            EndAtUtc = scheduleEvent.EndAt,
            Timezone = string.IsNullOrWhiteSpace(scheduleEvent.Timezone) ? DefaultTimezone : scheduleEvent.Timezone,
            Location = scheduleEvent.Location,
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
