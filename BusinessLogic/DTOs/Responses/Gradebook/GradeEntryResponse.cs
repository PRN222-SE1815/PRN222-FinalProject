namespace BusinessLogic.DTOs.Responses.Gradebook;

public sealed class GradeEntryResponse
{
    public int GradeEntryId { get; set; }

    public int GradeItemId { get; set; }

    public int EnrollmentId { get; set; }

    public string StudentCode { get; set; } = string.Empty;

    public string StudentName { get; set; } = string.Empty;

    public decimal? Score { get; set; }

    public DateTime UpdatedAt { get; set; }
}
