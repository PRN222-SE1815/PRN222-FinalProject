namespace BusinessLogic.DTOs.Responses.AI;

public sealed class AIChatMessageResponse
{
    public long ChatMessageId { get; set; }
    public long ChatSessionId { get; set; }
    public string SenderType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
