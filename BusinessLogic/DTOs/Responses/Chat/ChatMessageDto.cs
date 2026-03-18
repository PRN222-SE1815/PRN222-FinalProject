namespace BusinessLogic.DTOs.Responses.Chat;

public class ChatMessageDto
{
    public long MessageId { get; set; }
    public int RoomId { get; set; }
    public int SenderId { get; set; }
    public string SenderName { get; set; } = null!;
    public string MessageType { get; set; } = null!;
    public string? Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public List<ChatAttachmentDto> Attachments { get; set; } = new();
}
