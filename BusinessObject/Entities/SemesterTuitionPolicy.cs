using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BusinessObject.Entities;

public partial class SemesterTuitionPolicy
{
    [Key]
    public int PolicyId { get; set; }

    public int SemesterId { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal AmountPerCredit { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal DefaultSurcharge { get; set; }

    [StringLength(10)]
    public string CurrencyCode { get; set; } = null!;

    public bool IsActive { get; set; }

    [Precision(0)]
    public DateTime EffectiveFrom { get; set; }

    [Precision(0)]
    public DateTime? EffectiveTo { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [Precision(0)]
    public DateTime? UpdatedAt { get; set; }

    [InverseProperty("PricingPolicy")]
    public virtual ICollection<RegistrationOrder> RegistrationOrders { get; set; } = new List<RegistrationOrder>();

    [ForeignKey("SemesterId")]
    [InverseProperty("SemesterTuitionPolicy")]
    public virtual Semester Semester { get; set; } = null!;
}
