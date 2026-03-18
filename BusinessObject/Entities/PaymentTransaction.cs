using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BusinessObject.Entities;

[Index("MoMoOrderId", Name = "UX_PaymentTransactions_MoMoOrderId", IsUnique = true)]
public partial class PaymentTransaction
{
    [Key]
    public long TransactionId { get; set; }

    public int StudentId { get; set; }

    [StringLength(50)]
    public string PaymentMethod { get; set; } = null!;

    [StringLength(100)]
    public string MoMoRequestId { get; set; } = null!;

    [StringLength(100)]
    public string MoMoOrderId { get; set; } = null!;

    public long? MoMoTransId { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Amount { get; set; }

    [StringLength(500)]
    public string? OrderInfo { get; set; }

    [StringLength(20)]
    public string Status { get; set; } = null!;

    public int? ErrorCode { get; set; }

    [StringLength(500)]
    public string? LocalMessage { get; set; }

    public string? RawResponse { get; set; }

    [Precision(0)]
    public DateTime? PaymentDate { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("StudentId")]
    [InverseProperty("PaymentTransactions")]
    public virtual Student Student { get; set; } = null!;

    [InverseProperty("RelatedPayment")]
    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
}
