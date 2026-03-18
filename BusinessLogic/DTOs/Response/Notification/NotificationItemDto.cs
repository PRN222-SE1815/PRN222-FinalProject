namespace BusinessLogic.DTOs.Response;

public sealed class NotificationItemDto
{
    public long NotificationId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public bool IsRead { get; set; }
    public string? DeepLink { get; set; }
}
