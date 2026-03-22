namespace BusinessLogic.DTOs.Requests.GradeAppeals;

public sealed class UpdateGradeAppealResolutionData
{
    public long AppealId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ResponseMessage { get; set; }
    public int? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
