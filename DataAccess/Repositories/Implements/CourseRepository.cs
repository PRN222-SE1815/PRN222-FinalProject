using BusinessObject.Entities;
using BusinessObject.Enum;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataAccess.Repositories.Implements;

public sealed class CourseRepository : ICourseRepository
{
    private readonly SchoolManagementDbContext _context;
    private readonly ILogger<CourseRepository> _logger;

    public CourseRepository(SchoolManagementDbContext context, ILogger<CourseRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> CheckPrerequisiteSatisfiedAsync(int studentId, int courseId)
    {
        var prerequisiteIds = await _context.Courses
            .AsNoTracking()
            .Where(c => c.CourseId == courseId)
            .SelectMany(c => c.PrerequisiteCourses.Select(p => p.CourseId))
            .ToListAsync();

        if (prerequisiteIds.Count == 0)
        {
            return true;
        }

        var completedCount = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId
                && e.Status == EnrollmentStatus.COMPLETED.ToString()
                && prerequisiteIds.Contains(e.CourseId))
            .Select(e => e.CourseId)
            .Distinct()
            .CountAsync();

        return completedCount == prerequisiteIds.Count;
    }

    public async Task<IReadOnlyList<Course>> GetPrerequisiteCoursesAsync(int courseId)
    {
        return await _context.Courses
            .AsNoTracking()
            .Where(c => c.CourseId == courseId)
            .SelectMany(c => c.PrerequisiteCourses)
            .OrderBy(c => c.CourseCode)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Course>> GetMissingPrerequisiteCoursesAsync(int studentId, int courseId)
    {
        var prerequisiteCourses = await _context.Courses
            .AsNoTracking()
            .Where(c => c.CourseId == courseId)
            .SelectMany(c => c.PrerequisiteCourses)
            .ToListAsync();

        if (prerequisiteCourses.Count == 0)
        {
            return [];
        }

        var prerequisiteIds = prerequisiteCourses.Select(c => c.CourseId).ToList();

        var completedCourseIds = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId
                && e.Status == EnrollmentStatus.COMPLETED.ToString()
                && prerequisiteIds.Contains(e.CourseId))
            .Select(e => e.CourseId)
            .Distinct()
            .ToListAsync();

        return prerequisiteCourses
            .Where(c => !completedCourseIds.Contains(c.CourseId))
            .OrderBy(c => c.CourseCode)
            .ToList();
    }

    public async Task<(IReadOnlyList<Course> Items, int TotalCount)> GetPagedCoursesAsync(
        string? keyword,
        bool? isActive,
        int? semesterId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : pageSize;

        IQueryable<Course> query = _context.Courses.AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(c => c.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalizedKeyword = keyword.Trim().ToUpper();
            query = query.Where(c => c.CourseCode.ToUpper().Contains(normalizedKeyword)
                                     || c.CourseName.ToUpper().Contains(normalizedKeyword));
        }

        if (semesterId.HasValue)
        {
            query = query.Where(c => c.ClassSections.Any(cs => cs.SemesterId == semesterId.Value));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(c => c.CourseCode)
            .ThenBy(c => c.CourseId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public Task<Course?> GetByIdAsync(int courseId, CancellationToken ct = default)
    {
        return _context.Courses
            .AsNoTracking()
            .SingleOrDefaultAsync(c => c.CourseId == courseId, ct);
    }

    public Task<Course?> GetForUpdateAsync(int courseId, CancellationToken ct = default)
    {
        return _context.Courses
            .AsTracking()
            .SingleOrDefaultAsync(c => c.CourseId == courseId, ct);
    }

    public async Task<bool> ExistsByCodeAsync(string courseCode, int? excludeCourseId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(courseCode))
        {
            return false;
        }

        var normalizedCode = courseCode.Trim().ToUpper();

        var query = _context.Courses
            .AsNoTracking()
            .Where(c => c.CourseCode.ToUpper() == normalizedCode);

        if (excludeCourseId.HasValue)
        {
            query = query.Where(c => c.CourseId != excludeCourseId.Value);
        }

        return await query.AnyAsync(ct);
    }

    public async Task AddAsync(Course course, CancellationToken ct = default)
    {
        await _context.Courses.AddAsync(course, ct);
    }

    public void Update(Course course)
    {
        _context.Courses.Update(course);
    }

    public async Task<IReadOnlyList<ClassSection>> GetClassSectionsByCourseAsync(
        int courseId,
        bool asTracking,
        CancellationToken ct = default)
    {
        IQueryable<ClassSection> query = _context.ClassSections
            .Include(cs => cs.Semester)
            .Include(cs => cs.Teacher)
                .ThenInclude(t => t.TeacherNavigation)
            .Where(cs => cs.CourseId == courseId);

        query = asTracking ? query.AsTracking() : query.AsNoTracking();

        return await query
            .OrderByDescending(cs => cs.SemesterId)
            .ThenBy(cs => cs.SectionCode)
            .ThenBy(cs => cs.ClassSectionId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Enrollment>> GetEnrollmentsByCourseAsync(
        int courseId,
        IReadOnlyCollection<string>? statuses,
        bool asTracking,
        CancellationToken ct = default)
    {
        IQueryable<Enrollment> query = _context.Enrollments
            .Include(e => e.Student)
                .ThenInclude(s => s.StudentNavigation)
            .Include(e => e.ClassSection)
            .Where(e => e.CourseId == courseId);

        if (statuses is { Count: > 0 })
        {
            query = query.Where(e => statuses.Contains(e.Status));
        }

        query = asTracking ? query.AsTracking() : query.AsNoTracking();

        return await query
            .OrderBy(e => e.EnrollmentId)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<int>> GetImpactedStudentUserIdsAsync(int courseId, CancellationToken ct = default)
    {
        return await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.CourseId == courseId)
            .Select(e => e.StudentId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<int>> GetImpactedTeacherUserIdsAsync(int courseId, CancellationToken ct = default)
    {
        return await _context.ClassSections
            .AsNoTracking()
            .Where(cs => cs.CourseId == courseId)
            .Select(cs => cs.TeacherId)
            .Distinct()
            .ToListAsync(ct);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
    }
}
