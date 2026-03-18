namespace BusinessLogic.DTOs.Response;

public sealed class PrerequisiteInfoDto
{
    public int CourseId { get; set; }
    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
}
