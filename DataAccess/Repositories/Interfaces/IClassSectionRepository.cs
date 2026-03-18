using BusinessObject.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface IClassSectionRepository
{
    Task<ClassSection?> GetClassSectionForUpdateAsync(int classSectionId);
    Task<ClassSection?> GetClassSectionWithCourseAsync(int classSectionId);
    /// <summary>
    /// Check if a teacher (by UserId) is assigned to a specific class section.
    /// </summary>
    Task<bool> IsTeacherAssignedAsync(int teacherUserId, int classSectionId);

    /// <summary>
    /// Check if a teacher (by UserId) is assigned to any class section for a given course.
    /// </summary>
    Task<bool> IsTeacherAssignedToCourseAsync(int teacherUserId, int courseId);

    /// <summary>
    /// Get all class sections assigned to a teacher, including Course, Semester and GradeBook.
    /// </summary>
    Task<IReadOnlyList<ClassSection>> GetByTeacherIdAsync(int teacherUserId, CancellationToken ct = default);

    Task<IReadOnlyList<ClassSection>> GetByCourseIdAsync(
        int courseId,
        bool asTracking = false,
        CancellationToken ct = default);

    Task<int> CountOpenSectionsByCourseAsync(int courseId, CancellationToken ct = default);

    void UpdateRange(IEnumerable<ClassSection> sections);
}
