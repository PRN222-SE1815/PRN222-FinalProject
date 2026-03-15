using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

public partial class AIToolCall
{
    [Key]
    public long ToolCallId { get; set; }

    public long ChatSessionId { get; set; }

    [StringLength(200)]
    public string ToolName { get; set; } = null!;

    public string RequestJson { get; set; } = null!;

    public string? ResponseJson { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("ChatSessionId")]
    [InverseProperty("AIToolCalls")]
    public virtual AIChatSession ChatSession { get; set; } = null!;
}
