namespace BusinessLogic.DTOs.Requests.GradeAppeals;

public sealed class CreateGradeAppealData
{
    public int GradeBookId { get; set; }
    public int EnrollmentId { get; set; }
    public int StudentId { get; set; }
    public int? GradeItemId { get; set; }
    public string AppealContent { get; set; } = string.Empty;
    public string? EvidenceNote { get; set; }
}
