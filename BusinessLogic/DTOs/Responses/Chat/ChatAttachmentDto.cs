﻿namespace BusinessLogic.DTOs.Responses.Chat;

public class ChatAttachmentDto
{
    public long AttachmentId { get; set; }
    public long MessageId { get; set; }
    public string FileUrl { get; set; } = null!;
    public string FileType { get; set; } = null!;
    public long? FileSizeBytes { get; set; }
    public DateTime CreatedAt { get; set; }
    /// <summary>Display name shown to users. Derived from the FileUrl filename if not set.</summary>
    public string OriginalName { get; set; } = string.Empty;
}
