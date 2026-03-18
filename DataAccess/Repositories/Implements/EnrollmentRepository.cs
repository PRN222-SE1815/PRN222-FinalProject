using BusinessObject.Entities;
using BusinessObject.Enum;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataAccess.Repositories.Implements;

public sealed class EnrollmentRepository : IEnrollmentRepository
{
    private readonly SchoolManagementDbContext _context;
    private readonly ILogger<EnrollmentRepository> _logger;

    public EnrollmentRepository(SchoolManagementDbContext context, ILogger<EnrollmentRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<Enrollment?> GetExistingEnrollmentAsync(int studentId, int classSectionId, int semesterId)
    {
        return _context.Enrollments
            .AsNoTracking()
            .SingleOrDefaultAsync(e => e.StudentId == studentId
                && e.ClassSectionId == classSectionId
                && e.SemesterId == semesterId);
    }

    public Task<bool> HasTimeConflictAsync(int studentId, int semesterId, DateTime startAt, DateTime endAt)
    {
        var activeStatuses = new[]
        {
            EnrollmentStatus.ENROLLED.ToString(),
            EnrollmentStatus.PENDING_APPROVAL.ToString()
        };

        return _context.ScheduleEvents
            .AsNoTracking()
            .Where(se => se.ClassSection.Enrollments.Any(e => e.StudentId == studentId
                && e.SemesterId == semesterId
                && activeStatuses.Contains(e.Status)))
            .AnyAsync(se => startAt < se.EndAt && endAt > se.StartAt);
    }

    public async Task<int> GetCurrentCreditsAsync(int studentId, int semesterId)
    {
        var activeStatuses = new[]
        {
            EnrollmentStatus.ENROLLED.ToString(),
            EnrollmentStatus.PENDING_APPROVAL.ToString()
        };

        var totalCredits = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId
                && e.SemesterId == semesterId
                && activeStatuses.Contains(e.Status))
            .SumAsync(e => (int?)e.CreditsSnapshot);

        return totalCredits ?? 0;
    }

    public async Task AddEnrollmentAsync(Enrollment entity)
    {
        try
        {
            _context.Enrollments.Add(entity);
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "AddEnrollmentAsync DB error — StudentId={StudentId}, ClassSectionId={ClassSectionId}", entity.StudentId, entity.ClassSectionId);
            throw;
        }
    }

    public async Task<(IReadOnlyList<Enrollment> Items, int TotalCount)> GetStudentEnrollmentsAsync(
        int studentId,
        int? semesterId,
        int page,
        int pageSize)
    {
        var query = _context.Enrollments
            .AsNoTracking()
            .Include(e => e.Course)
            .Include(e => e.ClassSection)
                .ThenInclude(cs => cs.Teacher)
                    .ThenInclude(t => t.TeacherNavigation)
            .Include(e => e.Semester)
            .Where(e => e.StudentId == studentId);

        if (semesterId.HasValue)
        {
            query = query.Where(e => e.SemesterId == semesterId.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(e => e.SemesterId)
            .ThenBy(e => e.Course.CourseCode)
            .ThenBy(e => e.ClassSection.SectionCode)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<int?> GetEnrolledEnrollmentIdAsync(int studentUserId, int classSectionId)
    {
        // StudentId in Students/Enrollments = UserId in Users
        return await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentUserId
                        && e.ClassSectionId == classSectionId
                        && e.Status == "ENROLLED")
            .Select(e => (int?)e.EnrollmentId)
            .FirstOrDefaultAsync();
    }

    public async Task<bool> IsStudentEnrolledAsync(int studentUserId, int classSectionId)
    {
        return await _context.Enrollments
            .AsNoTracking()
            .AnyAsync(e => e.StudentId == studentUserId
                           && e.ClassSectionId == classSectionId
                           && e.Status == "ENROLLED");
    }

    public async Task<int?> GetStudentUserIdByEnrollmentIdAsync(int enrollmentId)
    {
        return await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.EnrollmentId == enrollmentId)
            .Select(e => (int?)e.StudentId)
            .FirstOrDefaultAsync();
    }

    public async Task<Enrollment?> GetEnrollmentBySectionAsync(int studentUserId, int classSectionId)
    {
        return await _context.Enrollments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.StudentId == studentUserId
                                      && e.ClassSectionId == classSectionId
                                      && e.Status == "ENROLLED");
    }

    public async Task<bool> IsStudentEnrolledInCourseAsync(int studentUserId, int courseId)
    {
        return await _context.Enrollments
            .AsNoTracking()
            .AnyAsync(e => e.StudentId == studentUserId
                           && e.CourseId == courseId
                           && e.Status == "ENROLLED");
    }

    public async Task<IReadOnlyList<int>> GetEnrolledStudentUserIdsAsync(int classSectionId, CancellationToken ct = default)
    {
        return await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.ClassSectionId == classSectionId && e.Status == "ENROLLED")
            .Select(e => e.StudentId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Enrollment>> GetByCourseIdAndStatusesAsync(
        int courseId,
        IReadOnlyCollection<string> statuses,
        bool asTracking = false,
        CancellationToken ct = default)
    {
        if (statuses is null || statuses.Count == 0)
        {
            return [];
        }

        IQueryable<Enrollment> query = _context.Enrollments
            .Include(e => e.Student)
            .Include(e => e.Course)
            .Include(e => e.ClassSection)
            .Where(e => e.CourseId == courseId && statuses.Contains(e.Status));

        query = asTracking ? query.AsTracking() : query.AsNoTracking();

        return await query
            .OrderBy(e => e.EnrollmentId)
            .ToListAsync(ct);
    }

    public async Task<int> CountByCourseIdAndStatusesAsync(
        int courseId,
        IReadOnlyCollection<string> statuses,
        CancellationToken ct = default)
    {
        if (statuses is null || statuses.Count == 0)
        {
            return 0;
        }

        return await _context.Enrollments
            .AsNoTracking()
            .CountAsync(e => e.CourseId == courseId && statuses.Contains(e.Status), ct);
    }

    public void UpdateRange(IEnumerable<Enrollment> enrollments)
    {
        _context.Enrollments.UpdateRange(enrollments);
    }
}
