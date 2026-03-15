using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

[Index("RoomId", "CreatedAt", Name = "IX_ChatMessages_Room_CreatedAt", IsDescending = new[] { false, true })]
public partial class ChatMessage
{
    [Key]
    public long MessageId { get; set; }

    public int RoomId { get; set; }

    public int SenderId { get; set; }

    [StringLength(20)]
    public string MessageType { get; set; } = null!;

    public string? Content { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [Precision(0)]
    public DateTime? EditedAt { get; set; }

    [Precision(0)]
    public DateTime? DeletedAt { get; set; }

    [InverseProperty("Message")]
    public virtual ICollection<ChatMessageAttachment> ChatMessageAttachments { get; set; } = new List<ChatMessageAttachment>();

    [InverseProperty("TargetMessage")]
    public virtual ICollection<ChatModerationLog> ChatModerationLogs { get; set; } = new List<ChatModerationLog>();

    [ForeignKey("RoomId")]
    [InverseProperty("ChatMessages")]
    public virtual ChatRoom Room { get; set; } = null!;

    [ForeignKey("SenderId")]
    [InverseProperty("ChatMessages")]
    public virtual User Sender { get; set; } = null!;
}
