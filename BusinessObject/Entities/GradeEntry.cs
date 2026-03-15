using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

[Index("GradeItemId", "EnrollmentId", Name = "UQ_GradeEntries_Item_Enrollment", IsUnique = true)]
public partial class GradeEntry
{
    [Key]
    public int GradeEntryId { get; set; }

    public int GradeItemId { get; set; }

    public int EnrollmentId { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal? Score { get; set; }

    public int? UpdatedBy { get; set; }

    [Precision(0)]
    public DateTime UpdatedAt { get; set; }

    [ForeignKey("EnrollmentId")]
    [InverseProperty("GradeEntries")]
    public virtual Enrollment Enrollment { get; set; } = null!;

    [InverseProperty("GradeEntry")]
    public virtual ICollection<GradeAuditLog> GradeAuditLogs { get; set; } = new List<GradeAuditLog>();

    [ForeignKey("GradeItemId")]
    [InverseProperty("GradeEntries")]
    public virtual GradeItem GradeItem { get; set; } = null!;

    [ForeignKey("UpdatedBy")]
    [InverseProperty("GradeEntries")]
    public virtual User? UpdatedByNavigation { get; set; }
}
