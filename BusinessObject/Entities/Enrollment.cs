using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

[Index("ClassSectionId", Name = "IX_Enrollments_ClassSection")]
public partial class Enrollment
{
    [Key]
    public int EnrollmentId { get; set; }

    public int StudentId { get; set; }

    public int ClassSectionId { get; set; }

    public int SemesterId { get; set; }

    public int CourseId { get; set; }

    public int CreditsSnapshot { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    [Precision(0)]
    public DateTime EnrolledAt { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("ClassSectionId")]
    [InverseProperty("Enrollments")]
    public virtual ClassSection ClassSection { get; set; } = null!;

    [ForeignKey("CourseId")]
    [InverseProperty("Enrollments")]
    public virtual Course Course { get; set; } = null!;

    [InverseProperty("Enrollment")]
    public virtual ICollection<GradeAppeal> GradeAppeals { get; set; } = new List<GradeAppeal>();

    [InverseProperty("Enrollment")]
    public virtual ICollection<GradeEntry> GradeEntries { get; set; } = new List<GradeEntry>();

    [InverseProperty("Enrollment")]
    public virtual ICollection<RegistrationOrderItem> RegistrationOrderItems { get; set; } = new List<RegistrationOrderItem>();

    [ForeignKey("SemesterId")]
    [InverseProperty("Enrollments")]
    public virtual Semester Semester { get; set; } = null!;

    [ForeignKey("StudentId")]
    [InverseProperty("Enrollments")]
    public virtual Student Student { get; set; } = null!;
}
