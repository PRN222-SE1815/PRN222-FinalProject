using System.ComponentModel.DataAnnotations;

namespace BusinessLogic.DTOs.Requests.AI;

public sealed class StartChatSessionRequest
{
    [Required]
    public required string Purpose { get; set; }

    public int? SemesterId { get; set; }
    public string? PromptVersion { get; set; }
}
