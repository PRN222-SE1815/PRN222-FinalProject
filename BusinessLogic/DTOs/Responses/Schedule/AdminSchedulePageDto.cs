namespace BusinessLogic.DTOs.Responses;

public sealed class AdminSchedulePageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<AdminScheduleItemDto> Items { get; set; } = [];
}
