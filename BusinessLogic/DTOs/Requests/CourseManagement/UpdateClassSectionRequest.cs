namespace BusinessLogic.DTOs.Requests.CourseManagement;

public sealed class UpdateClassSectionRequest
{
    public int ClassSectionId { get; set; }

    public string SectionCode { get; set; } = string.Empty;

    public string? Room { get; set; }

    public int MaxCapacity { get; set; }

    public bool IsOpen { get; set; }
}