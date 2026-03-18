using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataAccess.Repositories.Implements;

public sealed class GradeBookRepository : IGradeBookRepository
{
    private static readonly string[] GradebookEnrollmentStatuses = ["ENROLLED", "COMPLETED"];

    private readonly SchoolManagementDbContext _context;
    private readonly ILogger<GradeBookRepository> _logger;

    public GradeBookRepository(SchoolManagementDbContext context, ILogger<GradeBookRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<GradeBook?> GetByClassSectionIdAsync(int classSectionId, CancellationToken ct = default)
    {
        return _context.GradeBooks
            .AsNoTracking()
            .FirstOrDefaultAsync(gb => gb.ClassSectionId == classSectionId, ct);
    }

    public Task<GradeBook?> GetDetailByClassSectionIdAsync(int classSectionId, CancellationToken ct = default)
    {
        return _context.GradeBooks
            .AsNoTracking()
            .Include(gb => gb.ClassSection)
                .ThenInclude(cs => cs.Course)
            .Include(gb => gb.ClassSection)
                .ThenInclude(cs => cs.Semester)
            .Include(gb => gb.GradeItems)
                .ThenInclude(gi => gi.GradeEntries)
                    .ThenInclude(ge => ge.Enrollment)
                        .ThenInclude(e => e.Student)
                            .ThenInclude(s => s.StudentNavigation)
            .FirstOrDefaultAsync(gb => gb.ClassSectionId == classSectionId, ct);
    }

    public async Task<(IReadOnlyList<Enrollment> Items, int TotalCount)> GetEnrollmentsForGradeBookAsync(
        int classSectionId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 20 : pageSize;

        var query = _context.Enrollments
            .AsNoTracking()
            .Include(e => e.Student)
                .ThenInclude(s => s.StudentNavigation)
            .Where(e => e.ClassSectionId == classSectionId
                && GradebookEnrollmentStatuses.Contains(e.Status));

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(e => e.Student.StudentCode)
            .ThenBy(e => e.EnrollmentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<GradeItem>> GetGradeItemsAsync(int gradeBookId, CancellationToken ct = default)
    {
        return await _context.GradeItems
            .AsNoTracking()
            .Where(gi => gi.GradeBookId == gradeBookId)
            .OrderBy(gi => gi.SortOrder)
            .ThenBy(gi => gi.GradeItemId)
            .ToListAsync(ct);
    }

    public Task<GradeItem?> GetGradeItemByIdAsync(int gradeItemId, CancellationToken ct = default)
    {
        return _context.GradeItems
            .AsNoTracking()
            .FirstOrDefaultAsync(gi => gi.GradeItemId == gradeItemId, ct);
    }

    public async Task AddGradeItemAsync(GradeItem entity, CancellationToken ct = default)
    {
        await _context.GradeItems.AddAsync(entity, ct);
    }

    public void UpdateGradeItem(GradeItem entity)
    {
        _context.GradeItems.Update(entity);
    }

    public async Task DeleteGradeItemAsync(int gradeItemId, CancellationToken ct = default)
    {
        var gradeItem = await _context.GradeItems
            .Include(gi => gi.GradeEntries)
            .FirstOrDefaultAsync(gi => gi.GradeItemId == gradeItemId, ct);

        if (gradeItem is null)
        {
            return;
        }

        if (gradeItem.GradeEntries.Count > 0)
        {
            _context.GradeEntries.RemoveRange(gradeItem.GradeEntries);
        }

        _context.GradeItems.Remove(gradeItem);
    }

    public Task<GradeEntry?> GetGradeEntryAsync(int gradeItemId, int enrollmentId, CancellationToken ct = default)
    {
        return _context.GradeEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(ge => ge.GradeItemId == gradeItemId && ge.EnrollmentId == enrollmentId, ct);
    }

    public async Task UpsertGradeEntryAsync(GradeEntry entity, CancellationToken ct = default)
    {
        var existing = await _context.GradeEntries
            .FirstOrDefaultAsync(ge => ge.GradeItemId == entity.GradeItemId
                && ge.EnrollmentId == entity.EnrollmentId, ct);

        if (existing is null)
        {
            try
            {
                await _context.GradeEntries.AddAsync(entity, ct);
            }
            catch (DbUpdateException)
            {
                _context.ChangeTracker.Clear();
                existing = await _context.GradeEntries
                    .FirstOrDefaultAsync(ge => ge.GradeItemId == entity.GradeItemId
                        && ge.EnrollmentId == entity.EnrollmentId, ct);

                if (existing is not null)
                {
                    existing.Score = entity.Score;
                    existing.UpdatedBy = entity.UpdatedBy;
                    existing.UpdatedAt = entity.UpdatedAt;
                }
            }

            return;
        }

        existing.Score = entity.Score;
        existing.UpdatedBy = entity.UpdatedBy;
        existing.UpdatedAt = entity.UpdatedAt;
    }

    public async Task AddGradeBookApprovalAsync(GradeBookApproval entity, CancellationToken ct = default)
    {
        await _context.GradeBookApprovals.AddAsync(entity, ct);
    }

    public async Task<IReadOnlyList<GradeBookApproval>> GetApprovalsByGradeBookIdAsync(int gradeBookId, CancellationToken ct = default)
    {
        return await _context.GradeBookApprovals
            .AsNoTracking()
            .Include(a => a.RequestByNavigation)
            .Include(a => a.ResponseByNavigation)
            .Where(a => a.GradeBookId == gradeBookId)
            .OrderByDescending(a => a.RequestAt ?? DateTime.MinValue)
            .ThenByDescending(a => a.ApprovalId)
            .ToListAsync(ct);
    }

    public async Task AddGradeAuditLogAsync(GradeAuditLog entity, CancellationToken ct = default)
    {
        await _context.GradeAuditLogs.AddAsync(entity, ct);
    }

    public void UpdateGradeBook(GradeBook entity)
    {
        _context.GradeBooks.Update(entity);
    }

    public async Task<(IReadOnlyList<GradeBook> Items, int TotalCount)> GetPagedGradebooksAsync(
        string? statusFilter,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = page <= 0 ? 1 : page;
        pageSize = pageSize <= 0 ? 10 : pageSize;

        var query = _context.GradeBooks
            .AsNoTracking()
            .Include(gb => gb.ClassSection)
                .ThenInclude(cs => cs.Course)
            .Include(gb => gb.ClassSection)
                .ThenInclude(cs => cs.Semester)
            .Include(gb => gb.ClassSection)
                .ThenInclude(cs => cs.Teacher)
                    .ThenInclude(t => t.TeacherNavigation)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            query = query.Where(gb => gb.Status == statusFilter);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(gb => gb.UpdatedAt ?? gb.CreatedAt)
            .ThenByDescending(gb => gb.GradeBookId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public async Task SaveChangesAsync(CancellationToken ct = default)
    {
        await _context.SaveChangesAsync(ct);
        _logger.LogDebug("Saved gradebook repository changes.");
    }
}
