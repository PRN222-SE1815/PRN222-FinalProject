using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

public partial class ChatModerationLog
{
    [Key]
    public long ModerationLogId { get; set; }

    public int RoomId { get; set; }

    public int ActorUserId { get; set; }

    [StringLength(50)]
    public string Action { get; set; } = null!;

    public int? TargetUserId { get; set; }

    public long? TargetMessageId { get; set; }

    public string? MetadataJson { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("ActorUserId")]
    [InverseProperty("ChatModerationLogActorUsers")]
    public virtual User ActorUser { get; set; } = null!;

    [ForeignKey("RoomId")]
    [InverseProperty("ChatModerationLogs")]
    public virtual ChatRoom Room { get; set; } = null!;

    [ForeignKey("TargetMessageId")]
    [InverseProperty("ChatModerationLogs")]
    public virtual ChatMessage? TargetMessage { get; set; }

    [ForeignKey("TargetUserId")]
    [InverseProperty("ChatModerationLogTargetUsers")]
    public virtual User? TargetUser { get; set; }
}
