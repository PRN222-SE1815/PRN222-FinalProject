using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

public partial class RegistrationCart
{
    [Key]
    public long CartId { get; set; }

    public int StudentId { get; set; }

    public int SemesterId { get; set; }

    [StringLength(20)]
    public string CartStatus { get; set; } = null!;

    [Column(TypeName = "decimal(18, 2)")]
    public decimal EstimatedSubTotalAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal EstimatedSurchargeAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal EstimatedTotalAmount { get; set; }

    [Precision(0)]
    public DateTime? CheckedOutAt { get; set; }

    [Precision(0)]
    public DateTime? ExpiresAt { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAt { get; set; }

    [InverseProperty("Cart")]
    public virtual ICollection<RegistrationCartItem> RegistrationCartItems { get; set; } = new List<RegistrationCartItem>();

    [InverseProperty("Cart")]
    public virtual ICollection<RegistrationOrder> RegistrationOrders { get; set; } = new List<RegistrationOrder>();

    [ForeignKey("SemesterId")]
    [InverseProperty("RegistrationCarts")]
    public virtual Semester Semester { get; set; } = null!;

    [ForeignKey("StudentId")]
    [InverseProperty("RegistrationCarts")]
    public virtual Student Student { get; set; } = null!;
}
