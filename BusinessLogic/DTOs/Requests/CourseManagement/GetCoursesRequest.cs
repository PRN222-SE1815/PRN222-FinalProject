namespace BusinessLogic.DTOs.Requests.CourseManagement;

public sealed class GetCoursesRequest
{
    public string? Keyword { get; set; }
    public bool? IsActive { get; set; }
    public int? SemesterId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
