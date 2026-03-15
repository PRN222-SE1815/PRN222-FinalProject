using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

[Index("GradeBookId", "RequestAt", Name = "IX_GradeBookApprovals_GradeBookId_RequestAt", IsDescending = new[] { false, true })]
[Index("Outcome", Name = "IX_GradeBookApprovals_Outcome")]
public partial class GradeBookApproval
{
    [Key]
    public int ApprovalId { get; set; }

    public int GradeBookId { get; set; }

    public int RequestBy { get; set; }

    [Precision(0)]
    public DateTime? RequestAt { get; set; }

    [StringLength(500)]
    public string? RequestMessage { get; set; }

    public int? ResponseBy { get; set; }

    [Precision(0)]
    public DateTime? ResponseAt { get; set; }

    [StringLength(500)]
    public string? ResponseMessage { get; set; }

    [StringLength(20)]
    public string? Outcome { get; set; }

    [ForeignKey("GradeBookId")]
    [InverseProperty("GradeBookApprovals")]
    public virtual GradeBook GradeBook { get; set; } = null!;

    [ForeignKey("RequestBy")]
    [InverseProperty("GradeBookApprovalRequestByNavigations")]
    public virtual User RequestByNavigation { get; set; } = null!;

    [ForeignKey("ResponseBy")]
    [InverseProperty("GradeBookApprovalResponseByNavigations")]
    public virtual User? ResponseByNavigation { get; set; }
}
