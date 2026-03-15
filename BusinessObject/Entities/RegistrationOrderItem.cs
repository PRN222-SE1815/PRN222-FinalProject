using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

[Index("OrderId", Name = "IX_RegistrationOrderItems_OrderId")]
[Index("OrderId", "ClassSectionId", Name = "UQ_RegistrationOrderItems_Order_ClassSection", IsUnique = true)]
public partial class RegistrationOrderItem
{
    [Key]
    public long OrderItemId { get; set; }

    public long OrderId { get; set; }

    public int ClassSectionId { get; set; }

    public int CourseId { get; set; }

    public int? EnrollmentId { get; set; }

    public int CreditsSnapshot { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitPricePerCredit { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal LineSubTotalAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal LineSurchargeAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal LineTotalAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal RefundAmount { get; set; }

    [StringLength(30)]
    public string ItemStatus { get; set; } = null!;

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("ClassSectionId")]
    [InverseProperty("RegistrationOrderItems")]
    public virtual ClassSection ClassSection { get; set; } = null!;

    [ForeignKey("CourseId")]
    [InverseProperty("RegistrationOrderItems")]
    public virtual Course Course { get; set; } = null!;

    [ForeignKey("EnrollmentId")]
    [InverseProperty("RegistrationOrderItems")]
    public virtual Enrollment? Enrollment { get; set; }

    [ForeignKey("OrderId")]
    [InverseProperty("RegistrationOrderItems")]
    public virtual RegistrationOrder Order { get; set; } = null!;
}
