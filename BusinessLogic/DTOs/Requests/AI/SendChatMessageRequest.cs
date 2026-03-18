using System.ComponentModel.DataAnnotations;

namespace BusinessLogic.DTOs.Requests.AI;

public sealed class SendChatMessageRequest
{
    public long ChatSessionId { get; set; }

    [MaxLength(4000)]
    public required string Message { get; set; }

    public bool UseTools { get; set; } = true;
}
