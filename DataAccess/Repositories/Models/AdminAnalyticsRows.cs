namespace DataAccess.Repositories.Models;

public sealed class AdminRevenueByMonthRow
{
    public int Year { get; set; }

    public int Month { get; set; }

    public decimal Revenue { get; set; }
}

public sealed class AdminProgramEnrollmentCountRow
{
    public int? ProgramId { get; set; }

    public string ProgramName { get; set; } = string.Empty;

    public int EnrollmentCount { get; set; }
}

public sealed class AdminPublishedEnrollmentRow
{
    public int EnrollmentId { get; set; }

    public int CourseId { get; set; }

    public string CourseCode { get; set; } = string.Empty;

    public string CourseName { get; set; } = string.Empty;
}

public sealed class AdminEnrollmentGradeComponentRow
{
    public int EnrollmentId { get; set; }

    public decimal? Score { get; set; }

    public decimal MaxScore { get; set; }

    public decimal? Weight { get; set; }
}

public sealed class AdminCourseAppealSummaryRow
{
    public int CourseId { get; set; }

    public string CourseCode { get; set; } = string.Empty;

    public string CourseName { get; set; } = string.Empty;

    public int ApprovedCount { get; set; }

    public int RejectedCount { get; set; }
}
