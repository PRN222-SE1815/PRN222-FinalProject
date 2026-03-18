namespace BusinessLogic.DTOs.Requests.CourseManagement;

public sealed class DeactivateCourseRequest
{
    public int CourseId { get; set; }
    public string? Reason { get; set; }
    public bool DropActiveEnrollments { get; set; } = true;
}
