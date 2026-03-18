using System.Security.Claims;
using BusinessLogic.DTOs.Request;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Teacher.Pages.TeacherSchedule;

[Authorize(Roles = nameof(UserRole.TEACHER))]
public class IndexModel : PageModel
{
    private const int MaxRangeDays = 120;

    private readonly ITeacherScheduleService _teacherScheduleService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(ITeacherScheduleService teacherScheduleService, ILogger<IndexModel> logger)
    {
        _teacherScheduleService = teacherScheduleService;
        _logger = logger;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnGetEventsAsync(DateTime from, DateTime to, string? viewMode, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
        {
            _logger.LogWarning("TeacherSchedule events request unauthorized due to invalid NameIdentifier claim.");
            return Unauthorized();
        }

        if (from >= to)
        {
            _logger.LogWarning("TeacherSchedule invalid range. UserId={UserId}, From={From}, To={To}", userId, from, to);
            return BadRequest(new { errorCode = "INVALID_RANGE", message = "The 'from' value must be earlier than 'to'." });
        }

        var safeFrom = from;
        var safeTo = to;
        if (safeTo - safeFrom > TimeSpan.FromDays(MaxRangeDays))
        {
            safeTo = safeFrom.AddDays(MaxRangeDays);
        }

        var request = new GetStudentCalendarRequest
        {
            FromUtc = safeFrom.ToUniversalTime(),
            ToUtc = safeTo.ToUniversalTime(),
            Timezone = "Asia/Ho_Chi_Minh",
            ViewMode = viewMode?.Trim().ToUpperInvariant() switch
            {
                "TODAY" => CalendarViewMode.TODAY,
                "MONTH" => CalendarViewMode.MONTH,
                _ => CalendarViewMode.WEEK
            }
        };

        var result = await _teacherScheduleService.GetTeacherCalendarAsync(userId, request, cancellationToken);
        if (!result.IsSuccess)
        {
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
