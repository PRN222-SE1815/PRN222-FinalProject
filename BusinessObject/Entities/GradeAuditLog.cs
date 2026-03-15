using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

public partial class GradeAuditLog
{
    [Key]
    public int GradeAuditLogId { get; set; }

    public int GradeEntryId { get; set; }

    public int ActorUserId { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal? OldScore { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal? NewScore { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("ActorUserId")]
    [InverseProperty("GradeAuditLogs")]
    public virtual User ActorUser { get; set; } = null!;

    [ForeignKey("GradeEntryId")]
    [InverseProperty("GradeAuditLogs")]
    public virtual GradeEntry GradeEntry { get; set; } = null!;
}
