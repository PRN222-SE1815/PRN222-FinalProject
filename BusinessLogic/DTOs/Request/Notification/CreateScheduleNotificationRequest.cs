namespace BusinessLogic.DTOs.Request;

public sealed class CreateScheduleNotificationRequest
{
    public long ScheduleEventId { get; set; }
    public string ChangeType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<int> RecipientUserIds { get; set; } = Array.Empty<int>();
    public object? Payload { get; set; }
}
