using DataAccess.Repositories.Interfaces;
using DataAccess.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories.Implements;

public sealed class GradeBookExportReadRepository : IGradeBookExportReadRepository
{
    private readonly SchoolManagementDbContext _context;

    public GradeBookExportReadRepository(SchoolManagementDbContext context)
    {
        _context = context;
    }

    public Task<GradeBookExportMetaData?> GetExportMetaByClassSectionIdAsync(int classSectionId, CancellationToken ct = default)
    {
        return _context.GradeBooks
            .AsNoTracking()
            .Where(gb => gb.ClassSectionId == classSectionId)
            .Select(gb => new GradeBookExportMetaData
            {
                GradeBookId = gb.GradeBookId,
                ClassSectionId = gb.ClassSectionId,
                GradeBookStatus = gb.Status,
                TeacherId = gb.ClassSection.TeacherId,
                SemesterCode = gb.ClassSection.Semester.SemesterCode,
                CourseCode = gb.ClassSection.Course.CourseCode,
                SectionCode = gb.ClassSection.SectionCode
            })
            .SingleOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<GradeBookExportItemColumnData>> GetItemColumnsAsync(int gradeBookId, CancellationToken ct = default)
    {
        var items = await _context.GradeItems
            .AsNoTracking()
            .Where(gi => gi.GradeBookId == gradeBookId)
            .OrderBy(gi => gi.SortOrder)
            .ThenBy(gi => gi.GradeItemId)
            .Select(gi => new GradeBookExportItemColumnData
            {
                GradeItemId = gi.GradeItemId,
                ItemName = gi.ItemName,
                Weight = gi.Weight,
                SortOrder = gi.SortOrder
            })
            .ToListAsync(ct);

        return items;
    }

    public async Task<IReadOnlyList<GradeBookExportStudentRowRaw>> GetStudentRowsRawAsync(int classSectionId, CancellationToken ct = default)
    {
        var rows = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.ClassSectionId == classSectionId)
            .OrderBy(e => e.Student.StudentCode)
            .ThenBy(e => e.EnrollmentId)
            .Select(e => new GradeBookExportStudentRowRaw
            {
                EnrollmentId = e.EnrollmentId,
                StudentId = e.StudentId,
                StudentCode = e.Student.StudentCode,
                FullName = e.Student.StudentNavigation.FullName
            })
            .ToListAsync(ct);

        return rows;
    }

    public async Task<IReadOnlyList<GradeBookExportEntryRaw>> GetEntryRowsRawAsync(int gradeBookId, int classSectionId, CancellationToken ct = default)
    {
        var rows = await _context.GradeEntries
            .AsNoTracking()
            .Where(ge => ge.GradeItem.GradeBookId == gradeBookId
                && ge.Enrollment.ClassSectionId == classSectionId
                && ge.GradeItem.GradeBook.ClassSectionId == classSectionId)
            .Select(ge => new GradeBookExportEntryRaw
            {
                EnrollmentId = ge.EnrollmentId,
                GradeItemId = ge.GradeItemId,
                Score = ge.Score
            })
            .ToListAsync(ct);

        return rows;
    }
}
