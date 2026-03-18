﻿namespace BusinessLogic.DTOs.Requests.Chat;

/// <summary>
/// Attachment input when sending a message.
/// Items missing FileUrl or FileType will be skipped.
/// </summary>
public class ChatAttachmentInput
{
    public string? FileUrl { get; set; }
    public string? FileType { get; set; }
    public long? FileSizeBytes { get; set; }
    public string? OriginalName { get; set; }
}
