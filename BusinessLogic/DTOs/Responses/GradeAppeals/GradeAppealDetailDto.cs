namespace BusinessLogic.DTOs.Responses.GradeAppeals;

public sealed class GradeAppealDetailDto
{
    public long AppealId { get; set; }
    public int GradeBookId { get; set; }
    public int ClassSectionId { get; set; }
    public int EnrollmentId { get; set; }
    public int StudentId { get; set; }
    public int? GradeItemId { get; set; }
    public string AppealContent { get; set; } = string.Empty;
    public string? EvidenceNote { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ResponseMessage { get; set; }
    public int? ResolvedBy { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? StudentCode { get; set; }
    public string? StudentFullName { get; set; }
    public string? GradeBookStatus { get; set; }
    public string? ClassSectionCode { get; set; }
    public string? CourseCode { get; set; }
    public string? CourseName { get; set; }
    public string? GradeItemName { get; set; }
    public decimal? GradeItemScore { get; set; }
    public decimal? GradeItemMaxScore { get; set; }
    public string? ResolvedByFullName { get; set; }
}
