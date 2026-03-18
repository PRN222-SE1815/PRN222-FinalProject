namespace BusinessLogic.DTOs.Response;

public sealed class CalendarEventDto
{
    public long ScheduleEventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public string Timezone { get; set; } = "Asia/Ho_Chi_Minh";
    public string? Location { get; set; }
    public string? OnlineUrl { get; set; }
    public int ClassSectionId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string SemesterCode { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Color { get; set; } = "#1D4ED8";
    public bool IsRecurring { get; set; }
    public int? RecurrenceId { get; set; }
}
