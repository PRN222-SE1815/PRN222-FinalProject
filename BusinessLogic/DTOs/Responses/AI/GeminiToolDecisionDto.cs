namespace BusinessLogic.DTOs.Responses.AI;

public sealed class GeminiToolDecisionDto
{
    public bool RequiresToolCall { get; set; }
    public string? ToolName { get; set; }
    public string? ToolArgumentsJson { get; set; }
    public string? AssistantText { get; set; }
}
