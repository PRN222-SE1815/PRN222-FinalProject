namespace BusinessLogic.DTOs.GradeAppeals;

public sealed class GradeAppealQueryRequest
{
    public int? StudentId { get; set; }
    public int? SemesterId { get; set; }
    public int? GradeBookId { get; set; }
    public int? EnrollmentId { get; set; }
    public string? Status { get; set; }
    public DateTime? SubmittedFrom { get; set; }
    public DateTime? SubmittedTo { get; set; }
    public int? ResolvedBy { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
