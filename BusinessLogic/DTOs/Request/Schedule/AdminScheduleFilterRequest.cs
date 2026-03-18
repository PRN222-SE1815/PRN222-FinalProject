namespace BusinessLogic.DTOs.Request;

public sealed class AdminScheduleFilterRequest
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public DateTime? FromUtc { get; set; }
    public DateTime? ToUtc { get; set; }
    public int? SemesterId { get; set; }
    public int? ClassSectionId { get; set; }
    public int? TeacherId { get; set; }
    public string? Status { get; set; }
}
