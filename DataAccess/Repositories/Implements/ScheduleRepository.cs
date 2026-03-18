using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace DataAccess.Repositories.Implements;

public sealed class ScheduleRepository : IScheduleRepository
{
    private static readonly string[] DefaultStudentEnrollmentStatuses = ["ENROLLED"];
    private static readonly string[] StudentVisibleEventStatuses = ["PUBLISHED", "RESCHEDULED", "COMPLETED"];

    private readonly SchoolManagementDbContext _context;

    public ScheduleRepository(SchoolManagementDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ScheduleEvent>> GetStudentScheduleEventsAsync(
        int studentId,
        DateTime fromUtc,
        DateTime toUtc,
        IReadOnlyCollection<string>? allowedEnrollmentStatuses = null,
        CancellationToken cancellationToken = default)
    {
        if (studentId <= 0)
        {
            return [];
        }

        var statuses = (allowedEnrollmentStatuses is { Count: > 0 }
                ? allowedEnrollmentStatuses
                : DefaultStudentEnrollmentStatuses)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (statuses.Count == 0)
        {
            statuses = ["ENROLLED"];
        }

        var distinctEventIds = await (
            from se in _context.ScheduleEvents.AsNoTracking()
            join e in _context.Enrollments.AsNoTracking()
                on se.ClassSectionId equals e.ClassSectionId
            where e.StudentId == studentId
                  && statuses.Contains(e.Status)
                  && StudentVisibleEventStatuses.Contains(se.Status)
                  && se.EndAt > fromUtc
                  && se.StartAt < toUtc
            select se.ScheduleEventId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (distinctEventIds.Count == 0)
        {
            return [];
        }

        var items = await _context.ScheduleEvents
            .AsNoTracking()
            .Include(se => se.ClassSection)
                .ThenInclude(cs => cs.Course)
            .Include(se => se.ClassSection)
                .ThenInclude(cs => cs.Semester)
            .Include(se => se.Teacher)
                .ThenInclude(t => t!.TeacherNavigation)
            .Where(se => distinctEventIds.Contains(se.ScheduleEventId))
            .OrderBy(se => se.StartAt)
            .ThenBy(se => se.ScheduleEventId)
            .ToListAsync(cancellationToken);

        return items;
    }

    public async Task<IReadOnlyList<ScheduleEvent>> GetTeacherScheduleEventsAsync(
        int teacherId,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
    {
        if (teacherId <= 0)
        {
            return [];
        }

        return await _context.ScheduleEvents
            .AsNoTracking()
            .Include(se => se.ClassSection)
                .ThenInclude(cs => cs.Course)
            .Include(se => se.ClassSection)
                .ThenInclude(cs => cs.Semester)
            .Include(se => se.Teacher)
                .ThenInclude(t => t!.TeacherNavigation)
            .Where(se =>
                (se.TeacherId == teacherId || (se.TeacherId == null && se.ClassSection.TeacherId == teacherId))
                && se.EndAt > fromUtc
                && se.StartAt < toUtc)
            .OrderBy(se => se.StartAt)
            .ThenBy(se => se.ScheduleEventId)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<ScheduleEvent> Items, int TotalCount)> GetAdminScheduleEventsAsync(
        int page,
        int pageSize,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        int? semesterId = null,
        int? classSectionId = null,
        int? teacherId = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        if (page <= 0)
        {
            page = 1;
        }

        if (pageSize <= 0)
        {
            pageSize = 20;
        }

        var query = _context.ScheduleEvents
            .AsNoTracking()
            .Include(se => se.ClassSection)
                .ThenInclude(cs => cs.Course)
            .Include(se => se.ClassSection)
                .ThenInclude(cs => cs.Semester)
            .Include(se => se.Teacher)
                .ThenInclude(t => t!.TeacherNavigation)
            .Include(se => se.Recurrence)
            .AsQueryable();

        if (fromUtc.HasValue)
        {
            query = query.Where(se => se.EndAt > fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(se => se.StartAt < toUtc.Value);
        }

        if (semesterId.HasValue)
        {
            query = query.Where(se => se.ClassSection.SemesterId == semesterId.Value);
        }

        if (classSectionId.HasValue)
        {
            query = query.Where(se => se.ClassSectionId == classSectionId.Value);
        }

        if (teacherId.HasValue)
        {
            query = query.Where(se => se.TeacherId == teacherId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToUpper();
            query = query.Where(se => se.Status.ToUpper() == normalizedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(se => se.StartAt)
            .ThenByDescending(se => se.ScheduleEventId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<ScheduleEvent?> GetScheduleEventDetailAsync(
        long scheduleEventId,
        CancellationToken cancellationToken = default)
    {
        return _context.ScheduleEvents
            .AsNoTracking()
            .Include(se => se.ClassSection)
                .ThenInclude(cs => cs.Course)
            .Include(se => se.ClassSection)
                .ThenInclude(cs => cs.Semester)
            .Include(se => se.Teacher)
                .ThenInclude(t => t!.TeacherNavigation)
            .Include(se => se.Recurrence)
            .Include(se => se.ScheduleChangeLogs)
            .SingleOrDefaultAsync(se => se.ScheduleEventId == scheduleEventId, cancellationToken);
    }

    public async Task<ScheduleEvent> CreateScheduleEventAsync(
        ScheduleEvent scheduleEvent,
        ScheduleChangeLog? changeLog = null,
        CancellationToken cancellationToken = default)
    {
        _context.ScheduleEvents.Add(scheduleEvent);
        await _context.SaveChangesAsync(cancellationToken);

        if (changeLog is not null)
        {
            changeLog.ScheduleEventId = scheduleEvent.ScheduleEventId;
            _context.ScheduleChangeLogs.Add(changeLog);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return scheduleEvent;
    }

    public async Task<ScheduleEvent?> UpdateScheduleEventAsync(
        ScheduleEvent scheduleEvent,
        ScheduleChangeLog? changeLog = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.ScheduleEvents
            .SingleOrDefaultAsync(x => x.ScheduleEventId == scheduleEvent.ScheduleEventId, cancellationToken);

        if (existing is null)
        {
            return null;
        }

        existing.Title = scheduleEvent.Title;
        existing.StartAt = scheduleEvent.StartAt;
        existing.EndAt = scheduleEvent.EndAt;
        existing.Timezone = scheduleEvent.Timezone;
        existing.Location = scheduleEvent.Location;
        existing.OnlineUrl = scheduleEvent.OnlineUrl;
        existing.TeacherId = scheduleEvent.TeacherId;
        existing.RecurrenceId = scheduleEvent.RecurrenceId;
        existing.UpdatedBy = scheduleEvent.UpdatedBy;
        existing.UpdatedAt = scheduleEvent.UpdatedAt;

        if (changeLog is not null)
        {
            _context.ScheduleChangeLogs.Add(changeLog);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return existing;
    }

    public async Task<bool> ChangeScheduleStatusAsync(
        long scheduleEventId,
        string newStatus,
        int actorUserId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await _context.ScheduleEvents
            .SingleOrDefaultAsync(x => x.ScheduleEventId == scheduleEventId, cancellationToken);

        if (existing is null)
        {
            return false;
        }

        var oldStatus = existing.Status;
        existing.Status = newStatus;
        existing.UpdatedBy = actorUserId;
        existing.UpdatedAt = DateTime.UtcNow;

        _context.ScheduleChangeLogs.Add(new ScheduleChangeLog
        {
            ScheduleEventId = scheduleEventId,
            ActorUserId = actorUserId,
            ChangeType = string.Equals(newStatus, "CANCELLED", StringComparison.OrdinalIgnoreCase) ? "CANCEL" : "PUBLISH",
            OldJson = $"{{\"status\":\"{oldStatus}\"}}",
            NewJson = $"{{\"status\":\"{newStatus}\"}}",
            Reason = reason,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<int>> GetEnrolledStudentUserIdsByClassSectionAsync(
        int classSectionId,
        CancellationToken cancellationToken = default)
    {
        if (classSectionId <= 0)
        {
            return [];
        }

        return await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.ClassSectionId == classSectionId && e.Status == "ENROLLED")
            .Select(e => e.StudentId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ScheduleEventOverride>> GetOverridesByRecurrenceIdsAsync(
        IReadOnlyCollection<int> recurrenceIds,
        CancellationToken cancellationToken = default)
    {
        if (recurrenceIds is null || recurrenceIds.Count == 0)
        {
            return [];
        }

        var ids = recurrenceIds
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return [];
        }

        return await _context.ScheduleEventOverrides
            .AsNoTracking()
            .Where(o => ids.Contains(o.RecurrenceId))
            .OrderBy(o => o.OriginalDate)
            .ThenBy(o => o.OverrideId)
            .ToListAsync(cancellationToken);
    }
}
