using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BusinessObject.Entities;

[Index("CartId", "ClassSectionId", Name = "UQ_RegistrationCartItems_Cart_ClassSection", IsUnique = true)]
public partial class RegistrationCartItem
{
    [Key]
    public long CartItemId { get; set; }

    public long CartId { get; set; }

    public int ClassSectionId { get; set; }

    public int CourseId { get; set; }

    public int CreditsSnapshot { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal EstimatedUnitPricePerCredit { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal EstimatedLineAmount { get; set; }

    [Precision(0)]
    public DateTime AddedAt { get; set; }

    [ForeignKey("CartId")]
    [InverseProperty("RegistrationCartItems")]
    public virtual RegistrationCart Cart { get; set; } = null!;

    [ForeignKey("ClassSectionId")]
    [InverseProperty("RegistrationCartItems")]
    public virtual ClassSection ClassSection { get; set; } = null!;

    [ForeignKey("CourseId")]
    [InverseProperty("RegistrationCartItems")]
    public virtual Course Course { get; set; } = null!;
}
