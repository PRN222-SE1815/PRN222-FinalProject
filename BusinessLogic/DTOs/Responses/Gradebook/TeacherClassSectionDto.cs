namespace BusinessLogic.DTOs.Responses.Gradebook;

public sealed class TeacherClassSectionDto
{
    public int ClassSectionId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Credits { get; set; }
    public int SemesterId { get; set; }
    public string SemesterName { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public int MaxCapacity { get; set; }
    public int CurrentEnrollment { get; set; }
    public string? Room { get; set; }
    public string? GradebookStatus { get; set; }
}
