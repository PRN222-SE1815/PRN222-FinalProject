using BusinessObject.Entities;

namespace DataAccess.Repositories.Interfaces;

public interface IScheduleRepository
{
    // Student: lấy events thuộc các lớp student đã đăng ký trong khoảng thời gian
    Task<IReadOnlyList<ScheduleEvent>> GetStudentScheduleEventsAsync(
        int studentId,
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyCollection<string>? allowedEnrollmentStatuses = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ScheduleEvent>> GetTeacherScheduleEventsAsync(
        int teacherId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default);

    // Admin: lấy events theo filter + phân trang + stable sort
    Task<(IReadOnlyList<ScheduleEvent> Items, int TotalCount)> GetAdminScheduleEventsAsync(
        int page,
        int pageSize,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int? semesterId = null,
        int? classSectionId = null,
        int? teacherId = null,
        string? status = null,
        CancellationToken cancellationToken = default);

    // Detail 1 event cho màn hình admin edit
    Task<ScheduleEvent?> GetScheduleEventDetailAsync(
        long scheduleEventId,
        CancellationToken cancellationToken = default);

    Task<ScheduleEvent> CreateScheduleEventAsync(
        ScheduleEvent scheduleEvent,
        ScheduleChangeLog? changeLog = null,
        CancellationToken cancellationToken = default);

    Task<ScheduleEvent?> UpdateScheduleEventAsync(
        ScheduleEvent scheduleEvent,
        ScheduleChangeLog? changeLog = null,
        CancellationToken cancellationToken = default);

    Task<bool> ChangeScheduleStatusAsync(
        long scheduleEventId,
        string newStatus,
        int actorUserId,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetEnrolledStudentUserIdsByClassSectionAsync(
        int classSectionId,
        CancellationToken cancellationToken = default);

    // Lấy recurrence overrides theo recurrenceIds
    Task<IReadOnlyList<ScheduleEventOverride>> GetOverridesByRecurrenceIdsAsync(
        IReadOnlyCollection<int> recurrenceIds,
        CancellationToken cancellationToken = default);
}
