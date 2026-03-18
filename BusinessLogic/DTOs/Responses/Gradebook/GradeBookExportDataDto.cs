namespace BusinessLogic.DTOs.Responses.Gradebook;

public sealed class GradeBookExportDataDto
{
    public int GradeBookId { get; set; }

    public int ClassSectionId { get; set; }

    public string SemesterCode { get; set; } = string.Empty;

    public string CourseCode { get; set; } = string.Empty;

    public string SectionCode { get; set; } = string.Empty;

    public string GradeBookStatus { get; set; } = string.Empty;

    public IReadOnlyList<GradeExportItemColumnDto> Columns { get; set; } = [];

    public IReadOnlyList<GradeExportRowDto> Rows { get; set; } = [];

    public DateTime GeneratedAtUtc { get; set; }

    public string RequestedFormat { get; set; } = string.Empty;
}
