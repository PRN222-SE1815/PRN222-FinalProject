namespace BusinessLogic.DTOs.Response;

public sealed class MyCoursesPageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int? SemesterId { get; set; }
    public IReadOnlyList<MyCourseItemDto> Items { get; set; } = [];
    public IReadOnlyList<SemesterOptionDto> Semesters { get; set; } = [];
}
