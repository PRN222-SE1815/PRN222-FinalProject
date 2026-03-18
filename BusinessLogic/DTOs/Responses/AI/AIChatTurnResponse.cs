namespace BusinessLogic.DTOs.Responses.AI;

public sealed class AIChatTurnResponse
{
    public AIChatSessionResponse Session { get; set; } = new();
    public AIChatMessageResponse UserMessage { get; set; } = new();
    public AIChatMessageResponse AssistantMessage { get; set; } = new();
    public IReadOnlyList<AIToolCallResponse> ToolCalls { get; set; } = [];
    public string? Warning { get; set; }
    public AIResponseSchemaDto? StructuredData { get; set; }
}
