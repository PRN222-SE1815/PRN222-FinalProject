namespace BusinessLogic.DTOs.Requests.CourseManagement;

public sealed class DeleteClassSectionRequest
{
    public int ClassSectionId { get; set; }

    public string? Reason { get; set; }
}