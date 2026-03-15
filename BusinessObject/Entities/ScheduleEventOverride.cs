using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

public partial class ScheduleEventOverride
{
    [Key]
    public long OverrideId { get; set; }

    public int RecurrenceId { get; set; }

    public DateOnly OriginalDate { get; set; }

    [StringLength(20)]
    public string OverrideType { get; set; } = null!;

    [Precision(0)]
    public DateTime? NewStartAt { get; set; }

    [Precision(0)]
    public DateTime? NewEndAt { get; set; }

    [StringLength(200)]
    public string? NewLocation { get; set; }

    public int? NewTeacherId { get; set; }

    [StringLength(500)]
    public string? Reason { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("NewTeacherId")]
    [InverseProperty("ScheduleEventOverrides")]
    public virtual Teacher? NewTeacher { get; set; }

    [ForeignKey("RecurrenceId")]
    [InverseProperty("ScheduleEventOverrides")]
    public virtual Recurrence Recurrence { get; set; } = null!;
}
