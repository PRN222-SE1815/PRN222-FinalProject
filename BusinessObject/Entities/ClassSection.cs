using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

[Index("SemesterId", "CourseId", "SectionCode", Name = "UQ_ClassSections_Sem_Course_Section", IsUnique = true)]
public partial class ClassSection
{
    [Key]
    public int ClassSectionId { get; set; }

    public int SemesterId { get; set; }

    public int CourseId { get; set; }

    public int TeacherId { get; set; }

    [StringLength(50)]
    public string SectionCode { get; set; } = null!;

    public bool IsOpen { get; set; }

    public int MaxCapacity { get; set; }

    public int CurrentEnrollment { get; set; }

    [StringLength(100)]
    public string? Room { get; set; }

    [StringLength(500)]
    public string? OnlineUrl { get; set; }

    public string? Notes { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("ClassSection")]
    public virtual ICollection<ChatRoom> ChatRooms { get; set; } = new List<ChatRoom>();

    [ForeignKey("CourseId")]
    [InverseProperty("ClassSections")]
    public virtual Course Course { get; set; } = null!;

    [InverseProperty("ClassSection")]
    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    [InverseProperty("ClassSection")]
    public virtual GradeBook? GradeBook { get; set; }

    [InverseProperty("ClassSection")]
    public virtual ICollection<RegistrationCartItem> RegistrationCartItems { get; set; } = new List<RegistrationCartItem>();

    [InverseProperty("ClassSection")]
    public virtual ICollection<RegistrationOrderItem> RegistrationOrderItems { get; set; } = new List<RegistrationOrderItem>();

    [InverseProperty("ClassSection")]
    public virtual ICollection<ScheduleEvent> ScheduleEvents { get; set; } = new List<ScheduleEvent>();

    [ForeignKey("SemesterId")]
    [InverseProperty("ClassSections")]
    public virtual Semester Semester { get; set; } = null!;

    [ForeignKey("TeacherId")]
    [InverseProperty("ClassSections")]
    public virtual Teacher Teacher { get; set; } = null!;
}
