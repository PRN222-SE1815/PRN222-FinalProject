using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BusinessObject.Entities;

[Index("GradeBookId", "StudentId", Name = "UQ_GradeAppeals_GradeBook_Student", IsUnique = true)]
public partial class GradeAppeal
{
    [Key]
    public long AppealId { get; set; }

    public int GradeBookId { get; set; }

    public int EnrollmentId { get; set; }

    public int StudentId { get; set; }

    public int? GradeItemId { get; set; }

    [StringLength(1000)]
    public string AppealContent { get; set; } = null!;

    [StringLength(500)]
    public string? EvidenceNote { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    [StringLength(1000)]
    public string? ResponseMessage { get; set; }

    public int? ResolvedBy { get; set; }

    [Precision(0)]
    public DateTime SubmittedAt { get; set; }

    [Precision(0)]
    public DateTime? ResolvedAt { get; set; }

    [ForeignKey("EnrollmentId")]
    [InverseProperty("GradeAppeals")]
    public virtual Enrollment Enrollment { get; set; } = null!;

    [ForeignKey("GradeBookId")]
    [InverseProperty("GradeAppeals")]
    public virtual GradeBook GradeBook { get; set; } = null!;

    [ForeignKey("GradeItemId")]
    [InverseProperty("GradeAppeals")]
    public virtual GradeItem? GradeItem { get; set; }

    [ForeignKey("ResolvedBy")]
    [InverseProperty("GradeAppeals")]
    public virtual User? ResolvedByNavigation { get; set; }

    [ForeignKey("StudentId")]
    [InverseProperty("GradeAppeals")]
    public virtual Student Student { get; set; } = null!;
}
