namespace DataAccess.Repositories.Models;

public sealed class CourseCatalogRow
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Credits { get; set; }
    public bool IsOpen { get; set; }
    public int SectionCount { get; set; }
    public string? TeacherNames { get; set; }
}
