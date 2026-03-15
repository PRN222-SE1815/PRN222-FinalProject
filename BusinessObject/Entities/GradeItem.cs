using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

public partial class GradeItem
{
    [Key]
    public int GradeItemId { get; set; }

    public int GradeBookId { get; set; }

    [StringLength(200)]
    public string ItemName { get; set; } = null!;

    [Column(TypeName = "decimal(5, 2)")]
    public decimal MaxScore { get; set; }

    [Column(TypeName = "decimal(6, 4)")]
    public decimal? Weight { get; set; }

    public bool IsRequired { get; set; }

    public int SortOrder { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("GradeItem")]
    public virtual ICollection<GradeAppeal> GradeAppeals { get; set; } = new List<GradeAppeal>();

    [ForeignKey("GradeBookId")]
    [InverseProperty("GradeItems")]
    public virtual GradeBook GradeBook { get; set; } = null!;

    [InverseProperty("GradeItem")]
    public virtual ICollection<GradeEntry> GradeEntries { get; set; } = new List<GradeEntry>();
}
