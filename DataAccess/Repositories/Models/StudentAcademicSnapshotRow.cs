namespace DataAccess.Repositories.Models;

public sealed class StudentAcademicSnapshotRow
{
    public int SemesterId { get; set; }
    public string SemesterCode { get; set; } = string.Empty;
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public int Credits { get; set; }
    public string EnrollmentStatus { get; set; } = string.Empty;
    public decimal? FinalScore { get; set; }
    public bool? IsPassed { get; set; }
}
