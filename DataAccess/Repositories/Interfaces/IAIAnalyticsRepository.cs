using BusinessObject.Entities;
using DataAccess.Repositories.Models;

namespace DataAccess.Repositories.Interfaces;

public interface IAIAnalyticsRepository
{
    Task<Student?> GetStudentByUserIdAsync(int userId, CancellationToken ct = default);

    Task<IReadOnlyList<StudentAcademicSnapshotRow>> GetStudentAcademicSnapshotAsync(
        int studentId,
        int? semesterId,
        CancellationToken ct = default);

    Task<IReadOnlyList<CurrentEnrollmentRow>> GetCurrentEnrollmentsAsync(
        int studentId,
        int? semesterId,
        CancellationToken ct = default);

    Task<IReadOnlyList<CourseCatalogRow>> GetCourseCatalogAsync(
        int? programId,
        int semesterId,
        CancellationToken ct = default);

    Task<IReadOnlyList<PrerequisiteEdgeRow>> GetPrerequisiteGraphAsync(
        int? programId,
        CancellationToken ct = default);

    Task<IReadOnlyList<PlanConstraintCheckRow>> GetPlanConstraintDataAsync(
        int studentId,
        int semesterId,
        IReadOnlyCollection<int> candidateCourseIds,
        CancellationToken ct = default);
}
