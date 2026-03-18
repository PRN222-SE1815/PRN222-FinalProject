using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BusinessObject.Entities;

[Index("StudentId", Name = "UQ_StudentWallets_Student", IsUnique = true)]
public partial class StudentWallet
{
    [Key]
    public int WalletId { get; set; }

    public int StudentId { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Balance { get; set; }

    [StringLength(20)]
    public string WalletStatus { get; set; } = null!;

    [Precision(0)]
    public DateTime LastUpdated { get; set; }

    [ForeignKey("StudentId")]
    [InverseProperty("StudentWallet")]
    public virtual Student Student { get; set; } = null!;

    [InverseProperty("Wallet")]
    public virtual ICollection<WalletTransaction> WalletTransactions { get; set; } = new List<WalletTransaction>();
}
