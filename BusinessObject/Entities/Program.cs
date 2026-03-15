using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

[Index("ProgramCode", Name = "UQ_Programs_ProgramCode", IsUnique = true)]
public partial class Program
{
    [Key]
    public int ProgramId { get; set; }

    [StringLength(50)]
    public string ProgramCode { get; set; } = null!;

    [StringLength(200)]
    public string ProgramName { get; set; } = null!;

    public bool IsActive { get; set; }

    [InverseProperty("Program")]
    public virtual ICollection<Student> Students { get; set; } = new List<Student>();
}
