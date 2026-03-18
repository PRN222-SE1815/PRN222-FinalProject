namespace DataAccess.Repositories.Models;

public sealed class PrerequisiteEdgeRow
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int PrerequisiteCourseId { get; set; }
    public string PrerequisiteCourseCode { get; set; } = string.Empty;
    public string PrerequisiteCourseName { get; set; } = string.Empty;
}
