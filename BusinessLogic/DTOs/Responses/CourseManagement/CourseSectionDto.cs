namespace BusinessLogic.DTOs.Responses.CourseManagement;

public sealed class CourseSectionDto
{
    public int ClassSectionId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public string SemesterName { get; set; } = string.Empty;
    public int CurrentEnrollment { get; set; }
    public int MaxCapacity { get; set; }
    public bool IsOpen { get; set; }
    public string Room { get; set; } = string.Empty;
}
