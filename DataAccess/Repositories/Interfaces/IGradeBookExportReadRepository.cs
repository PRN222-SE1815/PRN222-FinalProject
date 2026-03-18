using DataAccess.Repositories.Models;

namespace DataAccess.Repositories.Interfaces;

public interface IGradeBookExportReadRepository
{
    Task<GradeBookExportMetaData?> GetExportMetaByClassSectionIdAsync(int classSectionId, CancellationToken ct = default);

    Task<IReadOnlyList<GradeBookExportItemColumnData>> GetItemColumnsAsync(int gradeBookId, CancellationToken ct = default);

    Task<IReadOnlyList<GradeBookExportStudentRowRaw>> GetStudentRowsRawAsync(int classSectionId, CancellationToken ct = default);

    Task<IReadOnlyList<GradeBookExportEntryRaw>> GetEntryRowsRawAsync(int gradeBookId, int classSectionId, CancellationToken ct = default);
}
