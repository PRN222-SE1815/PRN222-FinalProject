namespace BusinessLogic.DTOs.Responses.CourseManagement;

public sealed class DeactivateCourseResultResponse
{
    public int CourseId { get; set; }
    public int ClosedSectionCount { get; set; }
    public int DroppedEnrollmentCount { get; set; }
    public int AffectedStudentCount { get; set; }
    public int AffectedTeacherCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
