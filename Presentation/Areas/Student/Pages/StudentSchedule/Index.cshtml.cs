using System.Security.Claims;
using BusinessLogic.DTOs.Request;
using BusinessLogic.DTOs.Response;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Student.Pages.StudentSchedule;

[Authorize(Roles = nameof(UserRole.STUDENT))]
public class IndexModel : PageModel
{
    private const int MaxRangeDays = 120;

    private readonly IStudentScheduleService _studentScheduleService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IStudentScheduleService studentScheduleService, ILogger<IndexModel> logger)
    {
        _studentScheduleService = studentScheduleService;
        _logger = logger;
    }

    public void OnGet()
    {
        // TODO(manual-test): Login as student -> /Student/StudentSchedule/Index -> call ?handler=Events&from=...&to=...
    }

    public async Task<IActionResult> OnGetEventsAsync(DateTime from, DateTime to, string? viewMode, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            _logger.LogWarning("StudentSchedule events request unauthorized due to invalid NameIdentifier claim.");
            return Unauthorized();
        }

        if (from >= to)
        {
            _logger.LogWarning("StudentSchedule invalid range. UserId={UserId}, From={From}, To={To}", userId, from, to);
            return BadRequest(new { errorCode = "INVALID_RANGE", message = "The 'from' value must be earlier than 'to'." });
        }

        var safeFrom = from;
        var safeTo = to;
        var requestedRange = safeTo - safeFrom;
        if (requestedRange > TimeSpan.FromDays(MaxRangeDays))
        {
            safeTo = safeFrom.AddDays(MaxRangeDays);
            _logger.LogWarning(
                "StudentSchedule range clamped. UserId={UserId}, RequestedFrom={RequestedFrom}, RequestedTo={RequestedTo}, ClampedTo={ClampedTo}",
                userId,
                from,
                to,
                safeTo);
        }

        var calendarViewMode = viewMode?.Trim().ToUpperInvariant() switch
        {
            "TODAY" => CalendarViewMode.TODAY,
            "MONTH" => CalendarViewMode.MONTH,
            _ => CalendarViewMode.WEEK
        };

        _logger.LogInformation(
            "StudentSchedule events request. UserId={UserId}, From={From}, To={To}, ViewMode={ViewMode}",
            userId,
            safeFrom,
            safeTo,
            calendarViewMode);

        try
        {
            var request = new GetStudentCalendarRequest
            {
                FromUtc = safeFrom.ToUniversalTime(),
                ToUtc = safeTo.ToUniversalTime(),
                Timezone = "Asia/Ho_Chi_Minh",
                ViewMode = calendarViewMode
            };

            var result = await _studentScheduleService.GetStudentCalendarAsync(userId, request, cancellationToken);

            if (!result.IsSuccess)
            {
                _logger.LogWarning("GetStudentCalendarAsync failed. UserId={UserId}, ErrorCode={ErrorCode}, Message={Message}",
                    userId, result.ErrorCode, result.Message);
                return BadRequest(new { errorCode = result.ErrorCode, message = result.Message });
            }

            var events = (result.Data?.Events ?? [])
                .Select(e => new
                {
                    id = e.ScheduleEventId.ToString(),
                    title = e.Title,
                    start = e.StartAtUtc.ToString("yyyy-MM-ddTHH:mm:ss"),
                    end = e.EndAtUtc.ToString("yyyy-MM-ddTHH:mm:ss"),
                    backgroundColor = e.Color,
                    borderColor = e.Color,
                    classNames = new[] { $"status-{(e.Status ?? "unknown").ToLowerInvariant()}" },
                    extendedProps = new
                    {
                        courseCode = e.CourseCode,
                        courseName = e.CourseName,
                        sectionCode = e.SectionCode,
                        teacherName = e.TeacherName,
                        location = e.Location,
                        onlineUrl = e.OnlineUrl,
                        status = e.Status,
                        semesterCode = e.SemesterCode,
                        isRecurring = e.IsRecurring
                    }
                });

            return new JsonResult(events);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while loading student schedule events. UserId={UserId}", userId);
            return BadRequest(new { errorCode = "UNEXPECTED_ERROR", message = "Unable to load schedule events." });
        }
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;

        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim is null)
        {
            return false;
        }

        return int.TryParse(claim.Value, out userId) && userId > 0;
    }
}
