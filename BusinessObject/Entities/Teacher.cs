using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

[Index("TeacherCode", Name = "UQ_Teachers_TeacherCode", IsUnique = true)]
public partial class Teacher
{
    [Key]
    public int TeacherId { get; set; }

    [StringLength(50)]
    public string TeacherCode { get; set; } = null!;

    [StringLength(200)]
    public string? Department { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("Teacher")]
    public virtual ICollection<ClassSection> ClassSections { get; set; } = new List<ClassSection>();

    [InverseProperty("NewTeacher")]
    public virtual ICollection<ScheduleEventOverride> ScheduleEventOverrides { get; set; } = new List<ScheduleEventOverride>();

    [InverseProperty("Teacher")]
    public virtual ICollection<ScheduleEvent> ScheduleEvents { get; set; } = new List<ScheduleEvent>();

    [ForeignKey("TeacherId")]
    [InverseProperty("Teacher")]
    public virtual User TeacherNavigation { get; set; } = null!;
}
