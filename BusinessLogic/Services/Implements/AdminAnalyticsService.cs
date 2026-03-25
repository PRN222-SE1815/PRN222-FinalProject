using BusinessLogic.DTOs.Requests.AdminAnalytics;
using BusinessLogic.DTOs.Responses;
using BusinessLogic.DTOs.Responses.AdminAnalytics;
using BusinessLogic.Services.Interfaces;
using BusinessLogic.Services.Models;
using BusinessObject.Entities;
using BusinessObject.Enum;
using DataAccess.Repositories.Interfaces;
using DataAccess.Repositories.Models;
using Microsoft.Extensions.Logging;

namespace BusinessLogic.Services.Implements;

public sealed class AdminAnalyticsService : IAdminAnalyticsService
{
    private readonly IUserRepository _userRepository;
    private readonly ISemesterRepository _semesterRepository;
    private readonly IAdminAnalyticsRepository _adminAnalyticsRepository;
    private readonly IWeightedTotalCalculator _weightedTotalCalculator;
    private readonly ILogger<AdminAnalyticsService> _logger;

    public AdminAnalyticsService(
        IUserRepository userRepository,
        ISemesterRepository semesterRepository,
        IAdminAnalyticsRepository adminAnalyticsRepository,
        IWeightedTotalCalculator weightedTotalCalculator,
        ILogger<AdminAnalyticsService> logger)
    {
        _userRepository = userRepository;
        _semesterRepository = semesterRepository;
        _adminAnalyticsRepository = adminAnalyticsRepository;
        _weightedTotalCalculator = weightedTotalCalculator;
        _logger = logger;
    }

    public async Task<ServiceResult<AdminAnalyticsDashboardDto>> GetDashboardAsync(
        int adminUserId,
        AdminAnalyticsQueryRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (request is null)
            {
                request = new AdminAnalyticsQueryRequest();
            }

            var admin = await _userRepository.GetUserByIdAsync(adminUserId);
            if (admin is null || !admin.IsActive || !string.Equals(admin.Role, nameof(UserRole.ADMIN), StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<AdminAnalyticsDashboardDto>.Fail("FORBIDDEN", "Only admin can view analytics dashboard.");
            }

            var semesters = await _semesterRepository.GetAllSemestersAsync();
            var semesterOptions = semesters
                .OrderByDescending(s => s.StartDate)
                .Select(s => new AdminSemesterOptionDto
                {
                    SemesterId = s.SemesterId,
                    SemesterCode = s.SemesterCode,
                    SemesterName = s.SemesterName,
                    IsActive = s.IsActive
                })
                .ToList();

            Semester? selectedSemester = null;
            Semester? compareSemester = null;
            var includeAllSemesters = request.IncludeAllSemesters;

            if (!includeAllSemesters)
            {
                selectedSemester = ResolveSelectedSemester(semesters, request.SemesterId);
                if (selectedSemester is null)
                {
                    return ServiceResult<AdminAnalyticsDashboardDto>.Fail("SEMESTER_NOT_FOUND", "Cannot resolve selected semester.");
                }

                compareSemester = ResolveCompareSemester(semesters, selectedSemester, request.CompareSemesterId);
            }

            var selectedSemesterId = selectedSemester?.SemesterId;
            var compareSemesterId = compareSemester?.SemesterId;

            var totalEnrollments = await CreateKpiCardAsync(
                selectedSemesterId,
                compareSemesterId,
                async (semesterId, token) => await _adminAnalyticsRepository.CountEnrollmentsAsync(semesterId, token),
                ct);

            var totalRevenue = await CreateKpiCardAsync(
                selectedSemesterId,
                compareSemesterId,
                (semesterId, token) => _adminAnalyticsRepository.SumRegistrationRevenueAsync(semesterId, token),
                ct);

            var activeStudents = await CreateKpiCardAsync(
                selectedSemesterId,
                compareSemesterId,
                async (semesterId, token) => await _adminAnalyticsRepository.CountActiveStudentsAsync(semesterId, token),
                ct);

            var averagePassRate = await CreateKpiCardAsync(
                selectedSemesterId,
                compareSemesterId,
                async (semesterId, token) =>
                {
                    if (!semesterId.HasValue)
                    {
                        return 0m;
                    }

                    return await CalculatePassRateBySemesterAsync(semesterId.Value, token);
                },
                ct);

            var revenueChart = includeAllSemesters
                ? await BuildAllSemesterRevenueChartAsync(ct)
                : await BuildSemesterRevenueChartAsync(selectedSemester!, compareSemester, ct);

            var programGrowthChart = includeAllSemesters || selectedSemester is null || compareSemester is null
                ? new AdminProgramGrowthChartDto()
                : await BuildProgramGrowthChartAsync(selectedSemester.SemesterId, compareSemester.SemesterId, ct);

            var analyticsSemesterForTables = selectedSemester ?? semesters.OrderByDescending(s => s.StartDate).FirstOrDefault();
            var highestFailRateCourses = analyticsSemesterForTables is null
                ? []
                : await BuildHighestFailRateCoursesAsync(analyticsSemesterForTables.SemesterId, ct);

            var gradeAppealSummaries = analyticsSemesterForTables is null
                ? []
                : await BuildGradeAppealSummaryAsync(analyticsSemesterForTables.SemesterId, ct);

            var dashboard = new AdminAnalyticsDashboardDto
            {
                IncludeAllSemesters = includeAllSemesters,
                SelectedSemesterId = selectedSemesterId,
                CompareSemesterId = compareSemesterId,
                SelectedSemesterLabel = selectedSemester is null
                    ? "All Semesters"
                    : $"{selectedSemester.SemesterCode} - {selectedSemester.SemesterName}",
                CompareSemesterLabel = compareSemester is null
                    ? null
                    : $"{compareSemester.SemesterCode} - {compareSemester.SemesterName}",
                SemesterOptions = semesterOptions,
                TotalEnrollments = totalEnrollments,
                TotalRevenue = totalRevenue,
                AveragePassRate = averagePassRate,
                ActiveStudents = activeStudents,
                RevenueChart = revenueChart,
                ProgramGrowthChart = programGrowthChart,
                HighestFailRateCourses = highestFailRateCourses,
                GradeAppealSummaries = gradeAppealSummaries
            };

            return ServiceResult<AdminAnalyticsDashboardDto>.Success(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetDashboardAsync failed. AdminUserId={AdminUserId}", adminUserId);
            return ServiceResult<AdminAnalyticsDashboardDto>.Fail("SYSTEM_ERROR", "Unable to load analytics dashboard.");
        }
    }

    private static Semester? ResolveSelectedSemester(IReadOnlyList<Semester> semesters, int? requestedSemesterId)
    {
        if (semesters.Count == 0)
        {
            return null;
        }

        if (requestedSemesterId.HasValue)
        {
            return semesters.FirstOrDefault(s => s.SemesterId == requestedSemesterId.Value);
        }

        return semesters.FirstOrDefault(s => s.IsActive)
            ?? semesters.OrderByDescending(s => s.StartDate).FirstOrDefault();
    }

    private static Semester? ResolveCompareSemester(IReadOnlyList<Semester> semesters, Semester selectedSemester, int? requestedCompareSemesterId)
    {
        if (requestedCompareSemesterId.HasValue)
        {
            return semesters.FirstOrDefault(s => s.SemesterId == requestedCompareSemesterId.Value && s.SemesterId != selectedSemester.SemesterId);
        }

        return semesters
            .Where(s => s.StartDate < selectedSemester.StartDate)
            .OrderByDescending(s => s.StartDate)
            .FirstOrDefault();
    }

    private async Task<AdminKpiCardDto> CreateKpiCardAsync(
        int? selectedSemesterId,
        int? compareSemesterId,
        Func<int?, CancellationToken, Task<decimal>> provider,
        CancellationToken ct)
    {
        var current = await provider(selectedSemesterId, ct);

        decimal? previous = null;
        decimal? changePercent = null;

        if (compareSemesterId.HasValue)
        {
            previous = await provider(compareSemesterId, ct);

            if (previous.Value == 0m)
            {
                changePercent = current > 0m ? 100m : 0m;
            }
            else
            {
                changePercent = ((current - previous.Value) / previous.Value) * 100m;
            }
        }

        return new AdminKpiCardDto
        {
            CurrentValue = Math.Round(current, 2, MidpointRounding.AwayFromZero),
            PreviousValue = previous.HasValue
                ? Math.Round(previous.Value, 2, MidpointRounding.AwayFromZero)
                : null,
            ChangePercent = changePercent.HasValue
                ? Math.Round(changePercent.Value, 2, MidpointRounding.AwayFromZero)
                : null
        };
    }

    private async Task<decimal> CalculatePassRateBySemesterAsync(int semesterId, CancellationToken ct)
    {
        var enrollments = await _adminAnalyticsRepository.GetPublishedEnrollmentsBySemesterAsync(semesterId, ct);
        if (enrollments.Count == 0)
        {
            return 0m;
        }

        var components = await _adminAnalyticsRepository.GetEnrollmentGradeComponentsBySemesterAsync(semesterId, ct);
        var componentsByEnrollment = components
            .GroupBy(x => x.EnrollmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var passedCount = 0;

        foreach (var enrollment in enrollments)
        {
            componentsByEnrollment.TryGetValue(enrollment.EnrollmentId, out var enrollmentComponents);
            var total = CalculateEnrollmentTotal(enrollmentComponents);
            if (total >= 5m)
            {
                passedCount++;
            }
        }

        return Math.Round((decimal)passedCount * 100m / enrollments.Count, 2, MidpointRounding.AwayFromZero);
    }

    private decimal CalculateEnrollmentTotal(IReadOnlyCollection<AdminEnrollmentGradeComponentRow>? components)
    {
        if (components is null || components.Count == 0)
        {
            return 0m;
        }

        var weightedComponents = components
            .Where(x => x.Weight.HasValue && x.Weight.Value > 0m)
            .ToList();

        if (weightedComponents.Count > 0)
        {
            var weightedInputs = weightedComponents.Select(x => new WeightedScoreInput
            {
                Score = x.Score.HasValue && x.MaxScore > 0m
                    ? Math.Clamp((x.Score.Value / x.MaxScore) * 10m, 0m, 10m)
                    : 0m,
                Weight = x.Weight
            });

            return Math.Clamp(_weightedTotalCalculator.CalculateTotal(weightedInputs), 0m, 10m);
        }

        var normalizedScores = components
            .Where(x => x.Score.HasValue && x.MaxScore > 0m)
            .Select(x => Math.Clamp((x.Score!.Value / x.MaxScore) * 10m, 0m, 10m))
            .ToList();

        if (normalizedScores.Count == 0)
        {
            return 0m;
        }

        return Math.Round(normalizedScores.Average(), 2, MidpointRounding.AwayFromZero);
    }

    private async Task<AdminRevenueChartDto> BuildSemesterRevenueChartAsync(Semester selectedSemester, Semester? compareSemester, CancellationToken ct)
    {
        var selectedMonths = BuildMonthBuckets(selectedSemester.StartDate, selectedSemester.EndDate);
        var selectedRevenueRows = await _adminAnalyticsRepository.GetDepositRevenueByMonthAsync(
            selectedMonths.First(),
            selectedMonths.Last().AddMonths(1),
            ct);

        var selectedRevenueMap = selectedRevenueRows.ToDictionary(x => (x.Year, x.Month), x => x.Revenue);
        var labels = selectedMonths.Select(x => x.ToString("MMM")).ToList();
        var currentValues = selectedMonths
            .Select(x => Math.Round(selectedRevenueMap.GetValueOrDefault((x.Year, x.Month)), 2, MidpointRounding.AwayFromZero))
            .ToList();

        var compareValues = new List<decimal>();
        if (compareSemester is not null)
        {
            var compareMonths = BuildMonthBuckets(compareSemester.StartDate, compareSemester.EndDate);
            var compareRows = await _adminAnalyticsRepository.GetDepositRevenueByMonthAsync(
                compareMonths.First(),
                compareMonths.Last().AddMonths(1),
                ct);

            var compareMap = compareRows.ToDictionary(x => (x.Year, x.Month), x => x.Revenue);
            for (var i = 0; i < labels.Count; i++)
            {
                if (i < compareMonths.Count)
                {
                    var month = compareMonths[i];
                    compareValues.Add(Math.Round(compareMap.GetValueOrDefault((month.Year, month.Month)), 2, MidpointRounding.AwayFromZero));
                }
                else
                {
                    compareValues.Add(0m);
                }
            }
        }

        return new AdminRevenueChartDto
        {
            Labels = labels,
            CurrentValues = currentValues,
            CompareValues = compareValues
        };
    }

    private async Task<AdminRevenueChartDto> BuildAllSemesterRevenueChartAsync(CancellationToken ct)
    {
        var endMonth = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startMonth = endMonth.AddMonths(-11);

        var rows = await _adminAnalyticsRepository.GetDepositRevenueByMonthAsync(startMonth, endMonth.AddMonths(1), ct);
        var map = rows.ToDictionary(x => (x.Year, x.Month), x => x.Revenue);

        var labels = new List<string>();
        var values = new List<decimal>();

        for (var cursor = startMonth; cursor <= endMonth; cursor = cursor.AddMonths(1))
        {
            labels.Add(cursor.ToString("MMM yy"));
            values.Add(Math.Round(map.GetValueOrDefault((cursor.Year, cursor.Month)), 2, MidpointRounding.AwayFromZero));
        }

        return new AdminRevenueChartDto
        {
            Labels = labels,
            CurrentValues = values,
            CompareValues = []
        };
    }

    private async Task<AdminProgramGrowthChartDto> BuildProgramGrowthChartAsync(int selectedSemesterId, int compareSemesterId, CancellationToken ct)
    {
        var current = await _adminAnalyticsRepository.GetProgramEnrollmentCountsAsync(selectedSemesterId, ct);
        var previous = await _adminAnalyticsRepository.GetProgramEnrollmentCountsAsync(compareSemesterId, ct);

        var previousMap = previous.ToDictionary(x => x.ProgramId, x => x.EnrollmentCount);

        var growthRows = current
            .Select(c =>
            {
                var previousCount = previousMap.GetValueOrDefault(c.ProgramId);
                decimal growth;
                if (previousCount == 0)
                {
                    growth = c.EnrollmentCount > 0 ? 100m : 0m;
                }
                else
                {
                    growth = ((decimal)(c.EnrollmentCount - previousCount) / previousCount) * 100m;
                }

                return new
                {
                    c.ProgramName,
                    CurrentCount = c.EnrollmentCount,
                    PreviousCount = previousCount,
                    Growth = Math.Round(growth, 2, MidpointRounding.AwayFromZero)
                };
            })
            .OrderByDescending(x => x.Growth)
            .ThenByDescending(x => x.CurrentCount)
            .Take(5)
            .ToList();

        return new AdminProgramGrowthChartDto
        {
            Labels = growthRows.Select(x => x.ProgramName).ToList(),
            GrowthPercentValues = growthRows.Select(x => x.Growth).ToList(),
            CurrentEnrollmentValues = growthRows.Select(x => x.CurrentCount).ToList(),
            PreviousEnrollmentValues = growthRows.Select(x => x.PreviousCount).ToList()
        };
    }

    private async Task<IReadOnlyList<AdminCourseFailRateDto>> BuildHighestFailRateCoursesAsync(int semesterId, CancellationToken ct)
    {
        var enrollments = await _adminAnalyticsRepository.GetPublishedEnrollmentsBySemesterAsync(semesterId, ct);
        if (enrollments.Count == 0)
        {
            return [];
        }

        var components = await _adminAnalyticsRepository.GetEnrollmentGradeComponentsBySemesterAsync(semesterId, ct);
        var componentsByEnrollment = components
            .GroupBy(x => x.EnrollmentId)
            .ToDictionary(g => g.Key, g => (IReadOnlyCollection<AdminEnrollmentGradeComponentRow>)g.ToList());

        var outcomes = enrollments
            .Select(enrollment =>
            {
                componentsByEnrollment.TryGetValue(enrollment.EnrollmentId, out var enrollmentComponents);
                var total = CalculateEnrollmentTotal(enrollmentComponents);
                return new
                {
                    enrollment.CourseId,
                    enrollment.CourseCode,
                    enrollment.CourseName,
                    IsFailed = total < 5m
                };
            })
            .ToList();

        return outcomes
            .GroupBy(x => new { x.CourseId, x.CourseCode, x.CourseName })
            .Select(g => new AdminCourseFailRateDto
            {
                CourseId = g.Key.CourseId,
                CourseCode = g.Key.CourseCode,
                CourseName = g.Key.CourseName,
                TotalStudents = g.Count(),
                FailedStudents = g.Count(x => x.IsFailed),
                FailRatePercent = Math.Round((decimal)g.Count(x => x.IsFailed) * 100m / g.Count(), 2, MidpointRounding.AwayFromZero)
            })
            .OrderByDescending(x => x.FailRatePercent)
            .ThenByDescending(x => x.FailedStudents)
            .ThenBy(x => x.CourseCode)
            .Take(5)
            .ToList();
    }

    private async Task<IReadOnlyList<AdminCourseAppealSummaryDto>> BuildGradeAppealSummaryAsync(int semesterId, CancellationToken ct)
    {
        var rows = await _adminAnalyticsRepository.GetCourseAppealSummaryBySemesterAsync(semesterId, ct);

        return rows
            .Select(x => new AdminCourseAppealSummaryDto
            {
                CourseId = x.CourseId,
                CourseCode = x.CourseCode,
                CourseName = x.CourseName,
                ApprovedCount = x.ApprovedCount,
                RejectedCount = x.RejectedCount
            })
            .OrderByDescending(x => x.ApprovedCount + x.RejectedCount)
            .ThenBy(x => x.CourseCode)
            .Take(10)
            .ToList();
    }

    private static List<DateTime> BuildMonthBuckets(DateOnly fromDate, DateOnly toDate)
    {
        var from = new DateTime(fromDate.Year, fromDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(toDate.Year, toDate.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        if (to < from)
        {
            return [from];
        }

        var buckets = new List<DateTime>();
        for (var cursor = from; cursor <= to; cursor = cursor.AddMonths(1))
        {
            buckets.Add(cursor);
        }

        return buckets;
    }
}
