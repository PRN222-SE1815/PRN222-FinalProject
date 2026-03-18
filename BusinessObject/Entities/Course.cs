using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BusinessObject.Entities;

[Index("CourseCode", Name = "UQ_Courses_CourseCode", IsUnique = true)]
public partial class Course
{
    [Key]
    public int CourseId { get; set; }

    [StringLength(50)]
    public string CourseCode { get; set; } = null!;

    [StringLength(200)]
    public string CourseName { get; set; } = null!;

    public int Credits { get; set; }

    public string? Description { get; set; }

    public string? ContentHtml { get; set; }

    public string? LearningPathJson { get; set; }

    public bool IsActive { get; set; }

    [Precision(0)]
    public DateTime CreatedAt { get; set; }

    [InverseProperty("Course")]
    public virtual ICollection<ChatRoom> ChatRooms { get; set; } = new List<ChatRoom>();

    [InverseProperty("Course")]
    public virtual ICollection<ClassSection> ClassSections { get; set; } = new List<ClassSection>();

    [InverseProperty("Course")]
    public virtual ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();

    [InverseProperty("Course")]
    public virtual ICollection<RegistrationCartItem> RegistrationCartItems { get; set; } = new List<RegistrationCartItem>();

    [InverseProperty("Course")]
    public virtual ICollection<RegistrationOrderItem> RegistrationOrderItems { get; set; } = new List<RegistrationOrderItem>();

    [ForeignKey("PrerequisiteCourseId")]
    [InverseProperty("PrerequisiteCourses")]
    public virtual ICollection<Course> Courses { get; set; } = new List<Course>();

    [ForeignKey("CourseId")]
    [InverseProperty("Courses")]
    public virtual ICollection<Course> PrerequisiteCourses { get; set; } = new List<Course>();
}
