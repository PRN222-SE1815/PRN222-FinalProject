namespace BusinessLogic.DTOs.Response;

public sealed class AdminScheduleItemDto
{
    public long ScheduleEventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ClassSectionId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string SemesterCode { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
}
