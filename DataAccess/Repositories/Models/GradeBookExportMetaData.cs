namespace DataAccess.Repositories.Models;

public sealed class GradeBookExportMetaData
{
    public int GradeBookId { get; set; }

    public int ClassSectionId { get; set; }

    public string GradeBookStatus { get; set; } = string.Empty;

    public int TeacherId { get; set; }

    public string SemesterCode { get; set; } = string.Empty;

    public string CourseCode { get; set; } = string.Empty;

    public string SectionCode { get; set; } = string.Empty;
}
