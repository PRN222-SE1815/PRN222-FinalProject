namespace BusinessLogic.DTOs.Response;

public sealed class PendingEnrollmentViewModel
{
    public int EnrollmentId { get; set; }
    public int StudentId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string StudentFullName { get; set; } = string.Empty;
    public int ClassSectionId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string SectionCode { get; set; } = string.Empty;
    public int Credits { get; set; }
    public decimal FeeAmount { get; set; }
    public DateTime EnrolledAt { get; set; }
}
