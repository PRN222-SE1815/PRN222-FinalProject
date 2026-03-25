namespace BusinessLogic.DTOs.Responses.CourseManagement;

public sealed class ClassSectionDetailResponse
{
    public int ClassSectionId { get; set; }

    public int CourseId { get; set; }

    public int SemesterId { get; set; }

    public string SemesterName { get; set; } = string.Empty;

    public string SectionCode { get; set; } = string.Empty;

    public string Room { get; set; } = string.Empty;

    public int CurrentEnrollment { get; set; }

    public int MaxCapacity { get; set; }

    public bool IsOpen { get; set; }
}