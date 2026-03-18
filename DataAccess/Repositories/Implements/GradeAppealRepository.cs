using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;
using DataAccess.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories.Implements;

public sealed class GradeAppealRepository : IGradeAppealRepository
{
    private const int DefaultPageSize = 10;
    private const int MaxPageSize = 100;

    private readonly SchoolManagementDbContext _context;

    public GradeAppealRepository(SchoolManagementDbContext context)
    {
        _context = context;
    }

    public Task<GradeAppealDetailDto?> GetByIdAsync(long appealId, CancellationToken ct = default)
    {
        return _context.GradeAppeals
            .AsNoTracking()
            .Where(x => x.AppealId == appealId)
            .Select(x => new GradeAppealDetailDto
            {
                AppealId = x.AppealId,
                GradeBookId = x.GradeBookId,
                ClassSectionId = x.GradeBook.ClassSectionId,
                EnrollmentId = x.EnrollmentId,
                StudentId = x.StudentId,
                GradeItemId = x.GradeItemId,
                AppealContent = x.AppealContent,
                EvidenceNote = x.EvidenceNote,
                Status = x.Status,
                ResponseMessage = x.ResponseMessage,
                ResolvedBy = x.ResolvedBy,
                SubmittedAt = x.SubmittedAt,
                ResolvedAt = x.ResolvedAt,
                StudentCode = x.Student.StudentCode,
                StudentFullName = x.Student.StudentNavigation.FullName,
                GradeBookStatus = x.GradeBook.Status,
                ClassSectionCode = x.GradeBook.ClassSection.SectionCode,
                CourseCode = x.GradeBook.ClassSection.Course.CourseCode,
                CourseName = x.GradeBook.ClassSection.Course.CourseName,
                GradeItemName = x.GradeItem != null ? x.GradeItem.ItemName : null,
                GradeItemScore = x.GradeItemId.HasValue
                    ? x.Enrollment.GradeEntries
                        .Where(ge => ge.GradeItemId == x.GradeItemId.Value)
                        .Select(ge => ge.Score)
                        .FirstOrDefault()
                    : null,
                GradeItemMaxScore = x.GradeItem != null ? x.GradeItem.MaxScore : null,
                ResolvedByFullName = x.ResolvedByNavigation != null ? x.ResolvedByNavigation.FullName : null
            })
            .SingleOrDefaultAsync(ct);
    }

    public Task<GradeAppeal?> GetTrackedByIdAsync(long appealId, CancellationToken ct = default)
    {
        return _context.GradeAppeals
            .AsTracking()
            .SingleOrDefaultAsync(x => x.AppealId == appealId, ct);
    }

    public Task<Enrollment?> GetEnrollmentByIdAsync(int enrollmentId, CancellationToken ct = default)
    {
        return _context.Enrollments
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.EnrollmentId == enrollmentId, ct);
    }

    public Task<GradeBook?> GetGradeBookByIdAsync(int gradeBookId, CancellationToken ct = default)
    {
        return _context.GradeBooks
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.GradeBookId == gradeBookId, ct);
    }

    public Task<GradeEntry?> GetGradeEntryByIdAsync(int gradeEntryId, CancellationToken ct = default)
    {
        return _context.GradeEntries
            .AsTracking()
            .Include(x => x.GradeItem)
            .SingleOrDefaultAsync(x => x.GradeEntryId == gradeEntryId, ct);
    }

    public Task<bool> ExistsDuplicateAsync(int gradeBookId, int studentId, CancellationToken ct = default)
    {
        return _context.GradeAppeals
            .AsNoTracking()
            .AnyAsync(x => x.GradeBookId == gradeBookId && x.StudentId == studentId, ct);
    }

    public async Task<PagedResult<GradeAppealListItemDto>> GetPagedAsync(GradeAppealQueryRequest request, CancellationToken ct = default)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize <= 0 ? DefaultPageSize : Math.Min(request.PageSize, MaxPageSize);

        var query = _context.GradeAppeals.AsNoTracking();

        if (request.StudentId.HasValue)
        {
            query = query.Where(x => x.StudentId == request.StudentId.Value);
        }

        if (request.TeacherUserId.HasValue)
        {
            query = query.Where(x => x.GradeBook.ClassSection.TeacherId == request.TeacherUserId.Value);
        }

        if (request.SemesterId.HasValue)
        {
            query = query.Where(x => x.GradeBook.ClassSection.SemesterId == request.SemesterId.Value);
        }

        if (request.GradeBookId.HasValue)
        {
            query = query.Where(x => x.GradeBookId == request.GradeBookId.Value);
        }

        if (request.EnrollmentId.HasValue)
        {
            query = query.Where(x => x.EnrollmentId == request.EnrollmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = request.Status.Trim();
            query = query.Where(x => x.Status == status);
        }

        if (request.SubmittedFrom.HasValue)
        {
            query = query.Where(x => x.SubmittedAt >= request.SubmittedFrom.Value);
        }

        if (request.SubmittedTo.HasValue)
        {
            query = query.Where(x => x.SubmittedAt <= request.SubmittedTo.Value);
        }

        if (request.ResolvedBy.HasValue)
        {
            query = query.Where(x => x.ResolvedBy == request.ResolvedBy.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.SubmittedAt)
            .ThenByDescending(x => x.AppealId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new GradeAppealListItemDto
            {
                AppealId = x.AppealId,
                GradeBookId = x.GradeBookId,
                ClassSectionId = x.GradeBook.ClassSectionId,
                EnrollmentId = x.EnrollmentId,
                StudentId = x.StudentId,
                GradeItemId = x.GradeItemId,
                AppealContent = x.AppealContent,
                EvidenceNote = x.EvidenceNote,
                Status = x.Status,
                SubmittedAt = x.SubmittedAt,
                ResolvedAt = x.ResolvedAt,
                ResolvedBy = x.ResolvedBy,
                ResponseMessage = x.ResponseMessage,
                StudentCode = x.Student.StudentCode,
                StudentFullName = x.Student.StudentNavigation.FullName,
                GradeBookStatus = x.GradeBook.Status,
                ClassSectionCode = x.GradeBook.ClassSection.SectionCode,
                CourseCode = x.GradeBook.ClassSection.Course.CourseCode,
                CourseName = x.GradeBook.ClassSection.Course.CourseName,
                SemesterId = x.GradeBook.ClassSection.SemesterId,
                SemesterCode = x.GradeBook.ClassSection.Semester.SemesterCode,
                SemesterName = x.GradeBook.ClassSection.Semester.SemesterName,
                ResolvedByFullName = x.ResolvedByNavigation != null ? x.ResolvedByNavigation.FullName : null
            })
            .ToListAsync(ct);

        return new PagedResult<GradeAppealListItemDto>
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        };
    }

    public async Task AddAsync(GradeAppeal entity, CancellationToken ct = default)
    {
        await _context.GradeAppeals.AddAsync(entity, ct);
    }

    public Task UpdateAsync(GradeAppeal entity, CancellationToken ct = default)
    {
        _context.GradeAppeals.Update(entity);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
    {
        return _context.SaveChangesAsync(ct);
    }
}
