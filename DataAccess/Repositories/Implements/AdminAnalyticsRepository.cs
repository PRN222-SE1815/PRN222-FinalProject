using DataAccess.Repositories.Interfaces;
using DataAccess.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories.Implements;

public sealed class AdminAnalyticsRepository : IAdminAnalyticsRepository
{
    private static readonly string[] ExcludedEnrollmentStatuses =
    [
        "REJECTED",
        "CANCELED",
        "DROPPED",
        "WITHDRAWN"
    ];

    private readonly SchoolManagementDbContext _context;

    public AdminAnalyticsRepository(SchoolManagementDbContext context)
    {
        _context = context;
    }

    public Task<int> CountEnrollmentsAsync(int? semesterId, CancellationToken ct = default)
    {
        var query = _context.Enrollments
            .AsNoTracking()
            .Where(e => !ExcludedEnrollmentStatuses.Contains(e.Status));

        if (semesterId.HasValue)
        {
            query = query.Where(e => e.SemesterId == semesterId.Value);
        }

        return query.CountAsync(ct);
    }

    public async Task<int> CountActiveStudentsAsync(int? semesterId, CancellationToken ct = default)
    {
        var query = _context.Enrollments
            .AsNoTracking()
            .Where(e => !ExcludedEnrollmentStatuses.Contains(e.Status));

        if (semesterId.HasValue)
        {
            query = query.Where(e => e.SemesterId == semesterId.Value);
        }

        return await query
            .Select(e => e.StudentId)
            .Distinct()
            .CountAsync(ct);
    }

    public async Task<decimal> SumRegistrationRevenueAsync(int? semesterId, CancellationToken ct = default)
    {
        var query = _context.RegistrationOrders
            .AsNoTracking()
            .Where(o => o.PaidAt.HasValue && o.PaidAmount > 0m);

        if (semesterId.HasValue)
        {
            query = query.Where(o => o.SemesterId == semesterId.Value);
        }

        return await query
            .Select(o => (decimal?)o.PaidAmount)
            .SumAsync(ct) ?? 0m;
    }

    public async Task<IReadOnlyList<AdminRevenueByMonthRow>> GetDepositRevenueByMonthAsync(
        DateTime fromInclusiveUtc,
        DateTime toExclusiveUtc,
        CancellationToken ct = default)
    {
        return await _context.PaymentTransactions
            .AsNoTracking()
            .Where(p => p.Status == "SUCCESS"
                && p.PaymentDate.HasValue
                && p.PaymentDate.Value >= fromInclusiveUtc
                && p.PaymentDate.Value < toExclusiveUtc)
            .GroupBy(p => new
            {
                p.PaymentDate!.Value.Year,
                p.PaymentDate!.Value.Month
            })
            .Select(g => new AdminRevenueByMonthRow
            {
                Year = g.Key.Year,
                Month = g.Key.Month,
                Revenue = g.Sum(x => x.Amount)
            })
            .OrderBy(x => x.Year)
            .ThenBy(x => x.Month)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AdminProgramEnrollmentCountRow>> GetProgramEnrollmentCountsAsync(int semesterId, CancellationToken ct = default)
    {
        return await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.SemesterId == semesterId && !ExcludedEnrollmentStatuses.Contains(e.Status))
            .GroupBy(e => new
            {
                e.Student.ProgramId,
                ProgramName = e.Student.Program != null ? e.Student.Program.ProgramName : "Unassigned"
            })
            .Select(g => new AdminProgramEnrollmentCountRow
            {
                ProgramId = g.Key.ProgramId,
                ProgramName = g.Key.ProgramName,
                EnrollmentCount = g.Count()
            })
            .OrderByDescending(x => x.EnrollmentCount)
            .ThenBy(x => x.ProgramName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AdminPublishedEnrollmentRow>> GetPublishedEnrollmentsBySemesterAsync(int semesterId, CancellationToken ct = default)
    {
        return await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.SemesterId == semesterId
                && !ExcludedEnrollmentStatuses.Contains(e.Status)
                && e.ClassSection.GradeBook != null
                && e.ClassSection.GradeBook.Status == "PUBLISHED")
            .Select(e => new AdminPublishedEnrollmentRow
            {
                EnrollmentId = e.EnrollmentId,
                CourseId = e.CourseId,
                CourseCode = e.Course.CourseCode,
                CourseName = e.Course.CourseName
            })
            .OrderBy(x => x.CourseCode)
            .ThenBy(x => x.EnrollmentId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AdminEnrollmentGradeComponentRow>> GetEnrollmentGradeComponentsBySemesterAsync(int semesterId, CancellationToken ct = default)
    {
        return await _context.GradeEntries
            .AsNoTracking()
            .Where(ge => ge.Enrollment.SemesterId == semesterId
                && ge.Enrollment.ClassSection.GradeBook != null
                && ge.Enrollment.ClassSection.GradeBook.Status == "PUBLISHED")
            .Select(ge => new AdminEnrollmentGradeComponentRow
            {
                EnrollmentId = ge.EnrollmentId,
                Score = ge.Score,
                MaxScore = ge.GradeItem.MaxScore,
                Weight = ge.GradeItem.Weight
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AdminCourseAppealSummaryRow>> GetCourseAppealSummaryBySemesterAsync(int semesterId, CancellationToken ct = default)
    {
        return await _context.GradeAppeals
            .AsNoTracking()
            .Where(a => a.GradeBook.ClassSection.SemesterId == semesterId)
            .GroupBy(a => new
            {
                a.GradeBook.ClassSection.CourseId,
                a.GradeBook.ClassSection.Course.CourseCode,
                a.GradeBook.ClassSection.Course.CourseName
            })
            .Select(g => new AdminCourseAppealSummaryRow
            {
                CourseId = g.Key.CourseId,
                CourseCode = g.Key.CourseCode,
                CourseName = g.Key.CourseName,
                ApprovedCount = g.Count(x => x.Status == "APPROVED"),
                RejectedCount = g.Count(x => x.Status == "REJECTED")
            })
            .OrderByDescending(x => x.ApprovedCount + x.RejectedCount)
            .ThenBy(x => x.CourseCode)
            .ToListAsync(ct);
    }
}
