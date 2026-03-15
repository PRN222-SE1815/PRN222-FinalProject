using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

[Index("ClassSectionId", "StartAt", Name = "IX_ScheduleEvents_ClassSection_StartAt")]
public partial class ScheduleEvent
{
    [Key]
    public long ScheduleEventId { get; set; }

    public int ClassSectionId { get; set; }

    [StringLength(200)]
    public string Title { get; set; } = null!;

    [Precision(0)]
    public DateTime StartAt { get; set; }

    [Precision(0)]
    public DateTime EndAt { get; set; }

    [StringLength(100)]
    public string Timezone { get; set; } = null!;

    [StringLength(200)]
    public string? Location { get; set; }

    [StringLength(500)]
    public string? OnlineUrl { get; set; }

    public int? TeacherId { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    public int? RecurrenceId { get; set; }

    public int CreatedBy { get; set; }

    public int? UpdatedBy { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("ClassSectionId")]
    [InverseProperty("ScheduleEvents")]
    public virtual ClassSection ClassSection { get; set; } = null!;

    [ForeignKey("CreatedBy")]
    [InverseProperty("ScheduleEventCreatedByNavigations")]
    public virtual User CreatedByNavigation { get; set; } = null!;

    [ForeignKey("RecurrenceId")]
    [InverseProperty("ScheduleEvents")]
    public virtual Recurrence? Recurrence { get; set; }

    [InverseProperty("ScheduleEvent")]
    public virtual ICollection<ScheduleChangeLog> ScheduleChangeLogs { get; set; } = new List<ScheduleChangeLog>();

    [ForeignKey("TeacherId")]
    [InverseProperty("ScheduleEvents")]
    public virtual Teacher? Teacher { get; set; }

    [ForeignKey("UpdatedBy")]
    [InverseProperty("ScheduleEventUpdatedByNavigations")]
    public virtual User? UpdatedByNavigation { get; set; }
}
