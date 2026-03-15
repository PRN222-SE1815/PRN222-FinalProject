using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

public partial class AIChatSession
{
    [Key]
    public long ChatSessionId { get; set; }

    public int UserId { get; set; }

    [StringLength(50)]
    public string Purpose { get; set; } = null!;

    [StringLength(100)]
    public string? ModelName { get; set; }

    [StringLength(30)]
    public string State { get; set; } = null!;

    [StringLength(50)]
    public string? PromptVersion { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [Precision(0)]
    public DateTime? CompletedAt { get; set; }

    [InverseProperty("ChatSession")]
    public virtual ICollection<AIChatMessage> AIChatMessages { get; set; } = new List<AIChatMessage>();

    [InverseProperty("ChatSession")]
    public virtual ICollection<AIToolCall> AIToolCalls { get; set; } = new List<AIToolCall>();

    [ForeignKey("UserId")]
    [InverseProperty("AIChatSessions")]
    public virtual User User { get; set; } = null!;
}
