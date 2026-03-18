namespace BusinessLogic.DTOs.Responses.CourseManagement;

public sealed class CourseListItemResponse
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Credits { get; set; }
    public bool IsActive { get; set; }
    public int ClassSectionCount { get; set; }
    public IReadOnlyList<string> SemesterNames { get; set; } = [];
}
