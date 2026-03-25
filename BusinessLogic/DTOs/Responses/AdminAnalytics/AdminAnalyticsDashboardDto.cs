namespace BusinessLogic.DTOs.Responses.AdminAnalytics;

public sealed class AdminAnalyticsDashboardDto
{
    public bool IncludeAllSemesters { get; set; }

    public int? SelectedSemesterId { get; set; }

    public int? CompareSemesterId { get; set; }

    public string SelectedSemesterLabel { get; set; } = string.Empty;

    public string? CompareSemesterLabel { get; set; }

    public IReadOnlyList<AdminSemesterOptionDto> SemesterOptions { get; set; } = [];

    public AdminKpiCardDto TotalEnrollments { get; set; } = new();

    public AdminKpiCardDto TotalRevenue { get; set; } = new();

    public AdminKpiCardDto AveragePassRate { get; set; } = new();

    public AdminKpiCardDto ActiveStudents { get; set; } = new();

    public AdminRevenueChartDto RevenueChart { get; set; } = new();

    public AdminProgramGrowthChartDto ProgramGrowthChart { get; set; } = new();

    public IReadOnlyList<AdminCourseFailRateDto> HighestFailRateCourses { get; set; } = [];

    public IReadOnlyList<AdminCourseAppealSummaryDto> GradeAppealSummaries { get; set; } = [];
}

public sealed class AdminSemesterOptionDto
{
    public int SemesterId { get; set; }

    public string SemesterCode { get; set; } = string.Empty;

    public string SemesterName { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

public sealed class AdminKpiCardDto
{
    public decimal CurrentValue { get; set; }

    public decimal? PreviousValue { get; set; }

    public decimal? ChangePercent { get; set; }
}

public sealed class AdminRevenueChartDto
{
    public IReadOnlyList<string> Labels { get; set; } = [];

    public IReadOnlyList<decimal> CurrentValues { get; set; } = [];

    public IReadOnlyList<decimal> CompareValues { get; set; } = [];
}

public sealed class AdminProgramGrowthChartDto
{
    public IReadOnlyList<string> Labels { get; set; } = [];

    public IReadOnlyList<decimal> GrowthPercentValues { get; set; } = [];

    public IReadOnlyList<int> CurrentEnrollmentValues { get; set; } = [];

    public IReadOnlyList<int> PreviousEnrollmentValues { get; set; } = [];
}

public sealed class AdminCourseFailRateDto
{
    public int CourseId { get; set; }

    public string CourseCode { get; set; } = string.Empty;

    public string CourseName { get; set; } = string.Empty;

    public int TotalStudents { get; set; }

    public int FailedStudents { get; set; }

    public decimal FailRatePercent { get; set; }
}

public sealed class AdminCourseAppealSummaryDto
{
    public int CourseId { get; set; }

    public string CourseCode { get; set; } = string.Empty;

    public string CourseName { get; set; } = string.Empty;

    public int ApprovedCount { get; set; }

    public int RejectedCount { get; set; }
}
