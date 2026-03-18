using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataAccess.Repositories.Implements;

public sealed class ClassSectionRepository : IClassSectionRepository
{
    private readonly SchoolManagementDbContext _context;
    private readonly ILogger<ClassSectionRepository> _logger;

    public ClassSectionRepository(SchoolManagementDbContext context, ILogger<ClassSectionRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<ClassSection?> GetClassSectionForUpdateAsync(int classSectionId)
    {
        // TODO: lock at service via transaction isolation.
        return _context.ClassSections
            .AsTracking()
            .SingleOrDefaultAsync(cs => cs.ClassSectionId == classSectionId);
    }

    public Task<ClassSection?> GetClassSectionWithCourseAsync(int classSectionId)
    {
        return _context.ClassSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Semester)
            .SingleOrDefaultAsync(cs => cs.ClassSectionId == classSectionId);
    }

    public async Task<bool> IsTeacherAssignedAsync(int teacherUserId, int classSectionId)
    {
        // ClassSections.TeacherId = Teachers.TeacherId = Users.UserId
        return await _context.ClassSections
            .AsNoTracking()
            .AnyAsync(cs => cs.ClassSectionId == classSectionId && cs.TeacherId == teacherUserId);
    }

    public async Task<bool> IsTeacherAssignedToCourseAsync(int teacherUserId, int courseId)
    {
        return await _context.ClassSections
            .AsNoTracking()
            .AnyAsync(cs => cs.CourseId == courseId && cs.TeacherId == teacherUserId);
    }

    public async Task<IReadOnlyList<ClassSection>> GetByTeacherIdAsync(int teacherUserId, CancellationToken ct = default)
    {
        return await _context.ClassSections
            .AsNoTracking()
            .Include(cs => cs.Course)
            .Include(cs => cs.Semester)
            .Include(cs => cs.GradeBook)
            .Where(cs => cs.TeacherId == teacherUserId)
            .OrderByDescending(cs => cs.Semester.StartDate)
            .ThenBy(cs => cs.Course.CourseCode)
            .ThenBy(cs => cs.SectionCode)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ClassSection>> GetByCourseIdAsync(
        int courseId,
        bool asTracking = false,
        CancellationToken ct = default)
    {
        IQueryable<ClassSection> query = _context.ClassSections
            .Include(cs => cs.Semester)
            .Where(cs => cs.CourseId == courseId);

        query = asTracking ? query.AsTracking() : query.AsNoTracking();

        return await query
            .OrderByDescending(cs => cs.SemesterId)
            .ThenBy(cs => cs.SectionCode)
            .ThenBy(cs => cs.ClassSectionId)
            .ToListAsync(ct);
    }

    public Task<int> CountOpenSectionsByCourseAsync(int courseId, CancellationToken ct = default)
    {
        return _context.ClassSections
            .AsNoTracking()
            .CountAsync(cs => cs.CourseId == courseId && cs.IsOpen, ct);
    }

    public void UpdateRange(IEnumerable<ClassSection> sections)
    {
        _context.ClassSections.UpdateRange(sections);
    }
}
