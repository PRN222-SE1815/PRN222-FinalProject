namespace BusinessLogic.DTOs.Responses.AI;

public sealed class AIToolCallResponse
{
    public long ToolCallId { get; set; }
    public long ChatSessionId { get; set; }
    public string ToolName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
