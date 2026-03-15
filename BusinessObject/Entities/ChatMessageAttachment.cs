using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

public partial class ChatMessageAttachment
{
    [Key]
    public long AttachmentId { get; set; }

    public long MessageId { get; set; }

    [StringLength(1000)]
    public string FileUrl { get; set; } = null!;

    [StringLength(100)]
    public string FileType { get; set; } = null!;

    public long? FileSizeBytes { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("MessageId")]
    [InverseProperty("ChatMessageAttachments")]
    public virtual ChatMessage Message { get; set; } = null!;
}
