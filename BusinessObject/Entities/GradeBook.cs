using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

[Index("Status", "ClassSectionId", Name = "IX_GradeBooks_Status_ClassSection")]
[Index("ClassSectionId", Name = "UQ_GradeBooks_ClassSection", IsUnique = true)]
public partial class GradeBook
{
    [Key]
    public int GradeBookId { get; set; }

    public int ClassSectionId { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    public int Version { get; set; }

    [Precision(0)]
    public DateTime? PublishedAt { get; set; }

    [Precision(0)]
    public DateTime? LockedAt { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("ClassSectionId")]
    [InverseProperty("GradeBook")]
    public virtual ClassSection ClassSection { get; set; } = null!;

    [InverseProperty("GradeBook")]
    public virtual ICollection<GradeAppeal> GradeAppeals { get; set; } = new List<GradeAppeal>();

    [InverseProperty("GradeBook")]
    public virtual ICollection<GradeBookApproval> GradeBookApprovals { get; set; } = new List<GradeBookApproval>();

    [InverseProperty("GradeBook")]
    public virtual ICollection<GradeItem> GradeItems { get; set; } = new List<GradeItem>();
}
