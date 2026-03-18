namespace DataAccess.Repositories.Models;

public sealed class PlanConstraintCheckRow
{
    public int CandidateCourseId { get; set; }
    public string CandidateCourseCode { get; set; } = string.Empty;
    public string CandidateCourseName { get; set; } = string.Empty;
    public int CandidateCredits { get; set; }

    public int? PrerequisiteCourseId { get; set; }
    public string? PrerequisiteCourseCode { get; set; }
    public string? PrerequisiteCourseName { get; set; }
    public bool? IsPrerequisiteCompleted { get; set; }

    public int? ExistingEnrollmentId { get; set; }
    public int? ExistingClassSectionId { get; set; }
    public string? ExistingSectionCode { get; set; }
    public int? ExistingCourseId { get; set; }
    public string? ExistingCourseCode { get; set; }
    public long? ExistingScheduleEventId { get; set; }
    public DateTime? ExistingScheduleStartAt { get; set; }
    public DateTime? ExistingScheduleEndAt { get; set; }
    public string? ExistingScheduleStatus { get; set; }
}
