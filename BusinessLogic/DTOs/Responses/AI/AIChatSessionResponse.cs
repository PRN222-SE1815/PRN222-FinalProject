namespace BusinessLogic.DTOs.Responses.AI;

public sealed class AIChatSessionResponse
{
    public long ChatSessionId { get; set; }
    public int UserId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string? ModelName { get; set; }
    public string State { get; set; } = string.Empty;
    public string? PromptVersion { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}
