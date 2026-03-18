using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BusinessObject.Entities;

[Index("StudentCode", Name = "UQ_Students_StudentCode", IsUnique = true)]
public partial class Student
{
    [Key]
    public int StudentId { get; set; }

    [StringLength(50)]
    public string StudentCode { get; set; } = null!;

    public int? ProgramId { get; set; }

    public int? CurrentSemesterId { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [ForeignKey("CurrentSemesterId")]
    [InverseProperty("Students")]
    public virtual Semester? CurrentSemester { get; set; }

    [InverseProperty("Student")]
    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    [InverseProperty("Student")]
    public virtual ICollection<GradeAppeal> GradeAppeals { get; set; } = new List<GradeAppeal>();

    [InverseProperty("Student")]
    public virtual ICollection<PaymentTransaction> PaymentTransactions { get; set; } = new List<PaymentTransaction>();

    [ForeignKey("ProgramId")]
    [InverseProperty("Students")]
    public virtual Program? Program { get; set; }

    [InverseProperty("Student")]
    public virtual ICollection<RegistrationCart> RegistrationCarts { get; set; } = new List<RegistrationCart>();

    [InverseProperty("Student")]
    public virtual ICollection<RegistrationOrder> RegistrationOrders { get; set; } = new List<RegistrationOrder>();

    [ForeignKey("StudentId")]
    [InverseProperty("Student")]
    public virtual User StudentNavigation { get; set; } = null!;

    [InverseProperty("Student")]
    public virtual StudentWallet? StudentWallet { get; set; }
}
