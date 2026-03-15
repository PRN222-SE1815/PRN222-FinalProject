using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

[Index("StudentId", "CreatedAt", Name = "IX_RegistrationOrders_Student_CreatedAt", IsDescending = new[] { false, true })]
[Index("OrderCode", Name = "UQ_RegistrationOrders_OrderCode", IsUnique = true)]
public partial class RegistrationOrder
{
    [Key]
    public long OrderId { get; set; }

    public int StudentId { get; set; }

    public int SemesterId { get; set; }

    public long? CartId { get; set; }

    [StringLength(50)]
    public string OrderCode { get; set; } = null!;

    public int? PricingPolicyId { get; set; }

    [StringLength(30)]
    public string OrderStatus { get; set; } = null!;

    [Column(TypeName = "decimal(18, 2)")]
    public decimal SubTotalAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal SurchargeAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal PaidAmount { get; set; }

    [StringLength(500)]
    public string? FailureReason { get; set; }

    [Precision(0)]
    public DateTime? PaidAt { get; set; }

    [Precision(0)]
    public DateTime? CancelledAt { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey("CartId")]
    [InverseProperty("RegistrationOrders")]
    public virtual RegistrationCart? Cart { get; set; }

    [ForeignKey("PricingPolicyId")]
    [InverseProperty("RegistrationOrders")]
    public virtual SemesterTuitionPolicy? PricingPolicy { get; set; }

    [InverseProperty("Order")]
    public virtual ICollection<RegistrationOrderItem> RegistrationOrderItems { get; set; } = new List<RegistrationOrderItem>();

    [ForeignKey("SemesterId")]
    [InverseProperty("RegistrationOrders")]
    public virtual Semester Semester { get; set; } = null!;

    [ForeignKey("StudentId")]
    [InverseProperty("RegistrationOrders")]
    public virtual Student Student { get; set; } = null!;

    [InverseProperty("RelatedOrder")]
    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
}
