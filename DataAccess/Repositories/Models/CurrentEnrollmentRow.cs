namespace DataAccess.Repositories.Models;

public sealed class CurrentEnrollmentRow
{
    public int EnrollmentId { get; set; }
    public int SemesterId { get; set; }
    public string SemesterCode { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int ClassSectionId { get; set; }
    public string SectionCode { get; set; } = string.Empty;
    public string EnrollmentStatus { get; set; } = string.Empty;
    public int Credits { get; set; }
}
