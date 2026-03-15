using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

public partial class ScheduleChangeLog
{
    [Key]
    public long ChangeLogId { get; set; }

    public long ScheduleEventId { get; set; }

    public int ActorUserId { get; set; }

    [StringLength(50)]
    public string ChangeType { get; set; } = null!;

    public string? OldJson { get; set; }

    public string? NewJson { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("ActorUserId")]
    [InverseProperty("ScheduleChangeLogs")]
    public virtual User ActorUser { get; set; } = null!;

    [ForeignKey("ScheduleEventId")]
    [InverseProperty("ScheduleChangeLogs")]
    public virtual ScheduleEvent ScheduleEvent { get; set; } = null!;
}
