using BusinessObject.Enum;

namespace BusinessLogic.DTOs.Request;

public sealed class GetStudentCalendarRequest
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public string? Timezone { get; set; }
    public CalendarViewMode ViewMode { get; set; } = CalendarViewMode.WEEK;
}
