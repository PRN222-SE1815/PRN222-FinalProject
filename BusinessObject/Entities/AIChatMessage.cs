using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

public partial class AIChatMessage
{
    [Key]
    public long ChatMessageId { get; set; }

    public long ChatSessionId { get; set; }

    [StringLength(20)]
    public string SenderType { get; set; } = null!;

    public string Content { get; set; } = null!;

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("ChatSessionId")]
    [InverseProperty("AIChatMessages")]
    public virtual AIChatSession ChatSession { get; set; } = null!;
}
