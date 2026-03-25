using DataAccess.Repositories.Models;

namespace DataAccess.Repositories.Interfaces;

public interface IAdminAnalyticsRepository
{
    Task<int> CountEnrollmentsAsync(int? semesterId, CancellationToken ct = default);

    Task<int> CountActiveStudentsAsync(int? semesterId, CancellationToken ct = default);

    Task<decimal> SumRegistrationRevenueAsync(int? semesterId, CancellationToken ct = default);

    Task<IReadOnlyList<AdminRevenueByMonthRow>> GetDepositRevenueByMonthAsync(
        DateTime fromInclusiveUtc,
        DateTime toExclusiveUtc,
        CancellationToken ct = default);

    Task<IReadOnlyList<AdminProgramEnrollmentCountRow>> GetProgramEnrollmentCountsAsync(int semesterId, CancellationToken ct = default);

    Task<IReadOnlyList<AdminPublishedEnrollmentRow>> GetPublishedEnrollmentsBySemesterAsync(int semesterId, CancellationToken ct = default);

    Task<IReadOnlyList<AdminEnrollmentGradeComponentRow>> GetEnrollmentGradeComponentsBySemesterAsync(int semesterId, CancellationToken ct = default);

    Task<IReadOnlyList<AdminCourseAppealSummaryRow>> GetCourseAppealSummaryBySemesterAsync(int semesterId, CancellationToken ct = default);
}
