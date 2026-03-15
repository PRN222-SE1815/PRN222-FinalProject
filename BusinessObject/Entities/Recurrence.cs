using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

public partial class Recurrence
{
    [Key]
    public int RecurrenceId { get; set; }

    [StringLength(500)]
    public string RRule { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("Recurrence")]
    public virtual ICollection<ScheduleEventOverride> ScheduleEventOverrides { get; set; } = new List<ScheduleEventOverride>();

    [InverseProperty("Recurrence")]
    public virtual ICollection<ScheduleEvent> ScheduleEvents { get; set; } = new List<ScheduleEvent>();
}
