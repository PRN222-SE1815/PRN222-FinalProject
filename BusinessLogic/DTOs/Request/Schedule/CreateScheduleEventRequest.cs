namespace BusinessLogic.DTOs.Request;

public sealed class CreateScheduleEventRequest
{
    public int ClassSectionId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartAtUtc { get; set; }
    public DateTime EndAtUtc { get; set; }
    public string? Timezone { get; set; }
    public string? Location { get; set; }
    public string? OnlineUrl { get; set; }
    public int? TeacherId { get; set; }
    public int? RecurrenceId { get; set; }
    public string InitialStatus { get; set; } = "DRAFT";
    public string? Reason { get; set; }
}
