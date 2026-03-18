namespace BusinessLogic.DTOs.GradeAppeals;

public sealed class GradeAppealListItemDto
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
    public DateTime SubmittedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public int? ResolvedBy { get; set; }
    public string? ResponseMessage { get; set; }
    public string? StudentCode { get; set; }
    public string? StudentFullName { get; set; }
    public string? GradeBookStatus { get; set; }
    public string? ClassSectionCode { get; set; }
    public string? CourseCode { get; set; }
    public string? CourseName { get; set; }
    public int? SemesterId { get; set; }
    public string? SemesterCode { get; set; }
    public string? SemesterName { get; set; }
    public string? ResolvedByFullName { get; set; }
}
