namespace BusinessLogic.DTOs.Responses.Gradebook;

public sealed class GradebookApprovalResponse
{
    public int ApprovalId { get; set; }

    public int GradeBookId { get; set; }

    public string? Outcome { get; set; }

    public DateTime? RequestAt { get; set; }

    public DateTime? ResponseAt { get; set; }
}
