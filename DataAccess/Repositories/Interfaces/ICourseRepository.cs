using BusinessObject.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface ICourseRepository
{
    Task<bool> CheckPrerequisiteSatisfiedAsync(int studentId, int courseId);
    Task<IReadOnlyList<Course>> GetPrerequisiteCoursesAsync(int courseId);
    Task<IReadOnlyList<Course>> GetMissingPrerequisiteCoursesAsync(int studentId, int courseId);

    Task<(IReadOnlyList<Course> Items, int TotalCount)> GetPagedCoursesAsync(
        string? keyword,
        bool? isActive,
        int? semesterId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<Course?> GetByIdAsync(int courseId, CancellationToken ct = default);
    Task<Course?> GetForUpdateAsync(int courseId, CancellationToken ct = default);

    Task<bool> ExistsByCodeAsync(string courseCode, int? excludeCourseId = null, CancellationToken ct = default);

    Task AddAsync(Course course, CancellationToken ct = default);
    void Update(Course course);

    Task<IReadOnlyList<ClassSection>> GetClassSectionsByCourseAsync(
        int courseId,
        bool asTracking,
        CancellationToken ct = default);

    Task<IReadOnlyList<Enrollment>> GetEnrollmentsByCourseAsync(
        int courseId,
        IReadOnlyCollection<string>? statuses,
        bool asTracking,
        CancellationToken ct = default);

    Task<IReadOnlyList<int>> GetImpactedStudentUserIdsAsync(int courseId, CancellationToken ct = default);
    Task<IReadOnlyList<int>> GetImpactedTeacherUserIdsAsync(int courseId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);
}
