namespace BusinessLogic.DTOs.Requests.Gradebook;

public sealed class UpsertScoresRequest
{
    public int ClassSectionId { get; set; }

    public List<ScoreCellDto> Scores { get; set; } = [];
}

public sealed class ScoreCellDto
{
    public int GradeItemId { get; set; }

    public int EnrollmentId { get; set; }

    public decimal? Score { get; set; }

    public string? Reason { get; set; }
}
