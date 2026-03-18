namespace BusinessLogic.DTOs.Response;

public sealed class MyCourseItemDto
{
    public int EnrollmentId { get; set; }
    public int ClassSectionId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Credits { get; set; }
    public string SemesterCode { get; set; } = string.Empty;
    public string SectionCode { get; set; } = string.Empty;
    public string TeacherName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime EnrolledAt { get; set; }
}
