using System;
using BusinessObject.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface IEnrollmentRepository
{
    Task<Enrollment?> GetExistingEnrollmentAsync(int studentId, int classSectionId, int semesterId);
    Task<bool> HasTimeConflictAsync(int studentId, int semesterId, DateTime startAt, DateTime endAt);
    Task<int> GetCurrentCreditsAsync(int studentId, int semesterId);
    Task AddEnrollmentAsync(Enrollment entity);
    Task<(IReadOnlyList<Enrollment> Items, int TotalCount)> GetStudentEnrollmentsAsync(
        int studentId,
        int? semesterId,
        int page,
        int pageSize);
    /// <summary>
    /// Get EnrollmentId for a student in a class section with ENROLLED status.
    /// Returns null if not enrolled.
    /// </summary>
    Task<int?> GetEnrolledEnrollmentIdAsync(int studentUserId, int classSectionId);

    /// <summary>
    /// Check if student is enrolled (ENROLLED status) in a class section.
    /// </summary>
    Task<bool> IsStudentEnrolledAsync(int studentUserId, int classSectionId);

    /// <summary>
    /// Get StudentId (UserId) by EnrollmentId.
    /// </summary>
    Task<int?> GetStudentUserIdByEnrollmentIdAsync(int enrollmentId);

    /// <summary>
    /// Get enrollment for a student in a class section (by ClassSectionId).
    /// Returns the enrollment entity or null.
    /// </summary>
    Task<Enrollment?> GetEnrollmentBySectionAsync(int studentUserId, int classSectionId);

    /// <summary>
    /// Check if a student (by UserId) is enrolled (ENROLLED) in any class section of a course.
    /// </summary>
    Task<bool> IsStudentEnrolledInCourseAsync(int studentUserId, int courseId);

    /// <summary>
    /// Get all enrolled student UserIds for a class section.
    /// </summary>
    Task<IReadOnlyList<int>> GetEnrolledStudentUserIdsAsync(int classSectionId, CancellationToken ct = default);

    Task<IReadOnlyList<Enrollment>> GetByCourseIdAndStatusesAsync(
        int courseId,
        IReadOnlyCollection<string> statuses,
        bool asTracking = false,
        CancellationToken ct = default);

    Task<int> CountByCourseIdAndStatusesAsync(
        int courseId,
        IReadOnlyCollection<string> statuses,
        CancellationToken ct = default);

    void UpdateRange(IEnumerable<Enrollment> enrollments);
}
