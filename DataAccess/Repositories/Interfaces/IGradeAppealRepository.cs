using BusinessObject.Entities;
using DataAccess.Repositories.Models;

namespace DataAccess.Repositories.Interfaces;

public interface IGradeAppealRepository
{
    Task<GradeAppealDetailDto?> GetByIdAsync(long appealId, CancellationToken ct = default);
    Task<GradeAppeal?> GetTrackedByIdAsync(long appealId, CancellationToken ct = default);
    Task<Enrollment?> GetEnrollmentByIdAsync(int enrollmentId, CancellationToken ct = default);
    Task<GradeBook?> GetGradeBookByIdAsync(int gradeBookId, CancellationToken ct = default);
    Task<GradeEntry?> GetGradeEntryByIdAsync(int gradeEntryId, CancellationToken ct = default);
    Task<bool> ExistsDuplicateAsync(int gradeBookId, int studentId, CancellationToken ct = default);
    Task<PagedResult<GradeAppealListItemDto>> GetPagedAsync(GradeAppealQueryRequest request, CancellationToken ct = default);
    Task AddAsync(GradeAppeal entity, CancellationToken ct = default);
    Task UpdateAsync(GradeAppeal entity, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
