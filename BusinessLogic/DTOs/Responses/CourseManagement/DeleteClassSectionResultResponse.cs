namespace BusinessLogic.DTOs.Responses.CourseManagement;

public sealed class DeleteClassSectionResultResponse
{
    public int ClassSectionId { get; set; }

    public int CourseId { get; set; }

    public string Message { get; set; } = string.Empty;
}