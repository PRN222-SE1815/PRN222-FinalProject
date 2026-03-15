using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

[Index("SemesterCode", Name = "UQ_Semesters_SemesterCode", IsUnique = true)]
public partial class Semester
{
    [Key]
    public int SemesterId { get; set; }

    [StringLength(50)]
    public string SemesterCode { get; set; } = null!;

    [StringLength(200)]
    public string SemesterName { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public bool IsActive { get; set; }

    public DateOnly? RegistrationEndDate { get; set; }

    public DateOnly? AddDropDeadline { get; set; }

    public DateOnly? WithdrawalDeadline { get; set; }

    public int MaxCredits { get; set; }

    public int MinCredits { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("Semester")]
    public virtual ICollection<ClassSection> ClassSections { get; set; } = new List<ClassSection>();

    [InverseProperty("Semester")]
    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    [InverseProperty("Semester")]
    public virtual ICollection<RegistrationCart> RegistrationCarts { get; set; } = new List<RegistrationCart>();

    [InverseProperty("Semester")]
    public virtual ICollection<RegistrationOrder> RegistrationOrders { get; set; } = new List<RegistrationOrder>();

    [InverseProperty("Semester")]
    public virtual SemesterTuitionPolicy? SemesterTuitionPolicy { get; set; }

    [InverseProperty("CurrentSemester")]
    public virtual ICollection<Student> Students { get; set; } = new List<Student>();
}
