using BusinessObject.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface IGradeBookRepository
{
    Task<GradeBook?> GetByClassSectionIdAsync(int classSectionId, CancellationToken ct = default);

    Task<GradeBook?> GetDetailByClassSectionIdAsync(int classSectionId, CancellationToken ct = default);

    Task<(IReadOnlyList<Enrollment> Items, int TotalCount)> GetEnrollmentsForGradeBookAsync(
        int classSectionId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<IReadOnlyList<GradeItem>> GetGradeItemsAsync(int gradeBookId, CancellationToken ct = default);

    Task<GradeItem?> GetGradeItemByIdAsync(int gradeItemId, CancellationToken ct = default);

    Task AddGradeItemAsync(GradeItem entity, CancellationToken ct = default);

    void UpdateGradeItem(GradeItem entity);

    Task DeleteGradeItemAsync(int gradeItemId, CancellationToken ct = default);

    Task<GradeEntry?> GetGradeEntryAsync(int gradeItemId, int enrollmentId, CancellationToken ct = default);

    Task UpsertGradeEntryAsync(GradeEntry entity, CancellationToken ct = default);

    Task AddGradeBookApprovalAsync(GradeBookApproval entity, CancellationToken ct = default);

    Task<IReadOnlyList<GradeBookApproval>> GetApprovalsByGradeBookIdAsync(int gradeBookId, CancellationToken ct = default);

    Task AddGradeAuditLogAsync(GradeAuditLog entity, CancellationToken ct = default);

    void UpdateGradeBook(GradeBook entity);

    Task<(IReadOnlyList<GradeBook> Items, int TotalCount)> GetPagedGradebooksAsync(
        string? statusFilter,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
