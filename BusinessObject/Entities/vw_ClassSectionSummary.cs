using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Entities;

[Keyless]
public partial class vw_ClassSectionSummary
{
    public int ClassSectionId { get; set; }

    [StringLength(50)]
    public string SectionCode { get; set; } = null!;

    [StringLength(50)]
    public string SemesterCode { get; set; } = null!;

    [StringLength(50)]
    public string CourseCode { get; set; } = null!;

    [StringLength(200)]
    public string CourseName { get; set; } = null!;

    public int Credits { get; set; }

    public bool IsOpen { get; set; }

    public int CurrentEnrollment { get; set; }

    public int MaxCapacity { get; set; }

    [StringLength(200)]
    public string TeacherFullName { get; set; } = null!;
}
