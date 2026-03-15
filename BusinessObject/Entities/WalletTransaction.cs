using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

[Index("WalletId", "CreatedAt", Name = "IX_WalletTransactions_Wallet_CreatedAt", IsDescending = new[] { false, true })]
public partial class WalletTransaction
{
    [Key]
    public long WalletTransId { get; set; }

    public int WalletId { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Amount { get; set; }

    [StringLength(50)]
    public string TransactionType { get; set; } = null!;

    public long? RelatedPaymentId { get; set; }

    public long? RelatedOrderId { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? BalanceBefore { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? BalanceAfter { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("RelatedOrderId")]
    [InverseProperty("WalletTransactions")]
    public virtual RegistrationOrder? RelatedOrder { get; set; }

    [ForeignKey("RelatedPaymentId")]
    [InverseProperty("WalletTransactions")]
    public virtual PaymentTransaction? RelatedPayment { get; set; }

    [ForeignKey("WalletId")]
    [InverseProperty("WalletTransactions")]
    public virtual StudentWallet Wallet { get; set; } = null!;
}
