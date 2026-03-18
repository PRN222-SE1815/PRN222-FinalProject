namespace DataAccess.Repositories.Models;

public sealed class GradeBookExportStudentRowRaw
{
    public int EnrollmentId { get; set; }

    public int StudentId { get; set; }

    public string StudentCode { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
}
