namespace DataAccess.Repositories.Models;

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
    public string StudentCode { get; set; } = string.Empty;
    public string StudentFullName { get; set; } = string.Empty;
    public string GradeBookStatus { get; set; } = string.Empty;
    public string ClassSectionCode { get; set; } = string.Empty;
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string? GradeItemName { get; set; }
    public decimal? GradeItemScore { get; set; }
    public decimal? GradeItemMaxScore { get; set; }
    public string? ResolvedByFullName { get; set; }
}
