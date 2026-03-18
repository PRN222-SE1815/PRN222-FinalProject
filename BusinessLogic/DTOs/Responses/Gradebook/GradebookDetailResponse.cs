namespace BusinessLogic.DTOs.Responses.Gradebook;

public sealed class GradebookDetailResponse
{
    public int GradeBookId { get; set; }

    public int ClassSectionId { get; set; }

    public string SectionCode { get; set; } = string.Empty;

    public string CourseCode { get; set; } = string.Empty;

    public string CourseName { get; set; } = string.Empty;

    public string SemesterName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public int Version { get; set; }

    public IReadOnlyList<GradeItemResponse> GradeItems { get; set; } = [];

    public IReadOnlyList<GradeEntryResponse> GradeEntries { get; set; } = [];
}

public sealed class GradeItemResponse
{
    public int GradeItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public decimal MaxScore { get; set; }

    public decimal? Weight { get; set; }

    public bool IsRequired { get; set; }

    public int SortOrder { get; set; }
}
