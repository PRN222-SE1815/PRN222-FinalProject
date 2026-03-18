namespace BusinessLogic.DTOs.Response;

public sealed class StudentCalendarResponseDto
{
    public DateTime FromUtc { get; set; }
    public DateTime ToUtc { get; set; }
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
    public IReadOnlyList<CalendarEventDto> Events { get; set; } = [];
}
