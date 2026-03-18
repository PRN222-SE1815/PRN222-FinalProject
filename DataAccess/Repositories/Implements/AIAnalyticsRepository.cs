using BusinessObject.Entities;
using DataAccess.Repositories.Interfaces;
using DataAccess.Repositories.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataAccess.Repositories.Implements;

public sealed class AIAnalyticsRepository : IAIAnalyticsRepository
{
    private readonly SchoolManagementDbContext _context;
    private readonly ILogger<AIAnalyticsRepository> _logger;

    public AIAnalyticsRepository(SchoolManagementDbContext context, ILogger<AIAnalyticsRepository> logger)
    {
        _context = context;
        _logger = logger;
    }

    public Task<Student?> GetStudentByUserIdAsync(int userId, CancellationToken ct = default)
    {
        return _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StudentId == userId, ct);
    }

    public async Task<IReadOnlyList<StudentAcademicSnapshotRow>> GetStudentAcademicSnapshotAsync(
        int studentId,
        int? semesterId,
        CancellationToken ct = default)
    {
        var query = _context.Enrollments
            .AsNoTracking()
            .Include(e => e.Semester)
            .Include(e => e.Course)
            .Include(e => e.GradeEntries)
                .ThenInclude(ge => ge.GradeItem)
            .Where(e => e.StudentId == studentId);

        if (semesterId.HasValue)
        {
            query = query.Where(e => e.SemesterId == semesterId.Value);
        }

        var enrollments = await query.ToListAsync(ct);

        var rows = enrollments
            .Select(e =>
            {
                var scoredGradeEntries = e.GradeEntries
                    .Where(ge => ge.Score.HasValue)
                    .Select(ge => new
                    {
                        Score = ge.Score!.Value,
                        Weight = ge.GradeItem?.Weight
                    })
                    .ToList();

                decimal? finalScore = null;
                if (scoredGradeEntries.Count > 0)
                {
                    var weightedEntries = scoredGradeEntries.Where(x => x.Weight.HasValue).ToList();
                    var weightSum = weightedEntries.Sum(x => x.Weight!.Value);

                    if (weightSum > 0)
                    {
                        var weightedScore = weightedEntries.Sum(x => x.Score * x.Weight!.Value);
                        finalScore = weightedScore / weightSum;
                    }
                    else
                    {
                        finalScore = scoredGradeEntries.Average(x => x.Score);
                    }
                }

                bool? isPassed = finalScore.HasValue ? finalScore.Value >= 5m : null;

                return new StudentAcademicSnapshotRow
                {
                    SemesterId = e.SemesterId,
                    SemesterCode = e.Semester?.SemesterCode ?? string.Empty,
                    CourseId = e.CourseId,
                    CourseCode = e.Course?.CourseCode ?? string.Empty,
                    CourseName = e.Course?.CourseName ?? string.Empty,
                    Credits = e.CreditsSnapshot,
                    EnrollmentStatus = e.Status,
                    FinalScore = finalScore,
                    IsPassed = isPassed
                };
            })
            .OrderByDescending(x => x.SemesterId)
            .ThenBy(x => x.CourseCode)
            .ToList();

        return rows;
    }

    public async Task<IReadOnlyList<CurrentEnrollmentRow>> GetCurrentEnrollmentsAsync(
        int studentId,
        int? semesterId,
        CancellationToken ct = default)
    {
        var query = _context.Enrollments
            .AsNoTracking()
            .Include(e => e.Semester)
            .Include(e => e.Course)
            .Include(e => e.ClassSection)
            .Where(e => e.StudentId == studentId && e.Status == "ENROLLED");

        if (semesterId.HasValue)
        {
            query = query.Where(e => e.SemesterId == semesterId.Value);
        }

        var rows = await query
            .Select(e => new CurrentEnrollmentRow
            {
                EnrollmentId = e.EnrollmentId,
                SemesterId = e.SemesterId,
                SemesterCode = e.Semester.SemesterCode,
                CourseId = e.CourseId,
                CourseCode = e.Course.CourseCode,
                CourseName = e.Course.CourseName,
                ClassSectionId = e.ClassSectionId,
                SectionCode = e.ClassSection.SectionCode,
                EnrollmentStatus = e.Status,
                Credits = e.CreditsSnapshot
            })
            .OrderByDescending(x => x.SemesterId)
            .ThenBy(x => x.CourseCode)
            .ThenBy(x => x.SectionCode)
            .ToListAsync(ct);

        return rows;
    }

    public async Task<IReadOnlyList<CourseCatalogRow>> GetCourseCatalogAsync(
        int? programId,
        int semesterId,
        CancellationToken ct = default)
    {
        var coursesQuery = _context.Courses
            .AsNoTracking()
            .Where(c => c.IsActive);

        if (programId.HasValue)
        {
            coursesQuery = coursesQuery.Where(c => c.Enrollments.Any(e => e.Student.ProgramId == programId.Value));
        }

        var courses = await coursesQuery
            .OrderBy(c => c.CourseCode)
            .Select(c => new
            {
                c.CourseId,
                c.CourseCode,
                c.CourseName,
                c.Credits
            })
            .ToListAsync(ct);

        if (courses.Count == 0)
        {
            return [];
        }

        var courseIds = courses.Select(c => c.CourseId).ToHashSet();

        var sections = await _context.ClassSections
            .AsNoTracking()
            .Include(cs => cs.Teacher)
                .ThenInclude(t => t.TeacherNavigation)
            .Where(cs => cs.SemesterId == semesterId && courseIds.Contains(cs.CourseId))
            .Select(cs => new
            {
                cs.CourseId,
                cs.IsOpen,
                TeacherName = cs.Teacher.TeacherNavigation.FullName
            })
            .ToListAsync(ct);

        var sectionMap = sections
            .GroupBy(s => s.CourseId)
            .ToDictionary(
                g => g.Key,
                g => new
                {
                    SectionCount = g.Count(),
                    IsOpen = g.Any(x => x.IsOpen),
                    TeacherNames = string.Join(", ", g.Select(x => x.TeacherName)
                        .Where(n => !string.IsNullOrWhiteSpace(n))
                        .Distinct()
                        .OrderBy(n => n))
                });

        var rows = courses
            .Select(c =>
            {
                sectionMap.TryGetValue(c.CourseId, out var sectionData);

                return new CourseCatalogRow
                {
                    CourseId = c.CourseId,
                    CourseCode = c.CourseCode,
                    CourseName = c.CourseName,
                    Credits = c.Credits,
                    IsOpen = sectionData?.IsOpen ?? false,
                    SectionCount = sectionData?.SectionCount ?? 0,
                    TeacherNames = string.IsNullOrWhiteSpace(sectionData?.TeacherNames) ? null : sectionData!.TeacherNames
                };
            })
            .OrderBy(x => x.CourseCode)
            .ToList();

        return rows;
    }

    public async Task<IReadOnlyList<PrerequisiteEdgeRow>> GetPrerequisiteGraphAsync(int? programId, CancellationToken ct = default)
    {
        var courseQuery = _context.Courses
            .AsNoTracking()
            .Where(c => c.IsActive);

        if (programId.HasValue)
        {
            courseQuery = courseQuery.Where(c => c.Enrollments.Any(e => e.Student.ProgramId == programId.Value));
        }

        var rows = await courseQuery
            .SelectMany(c => c.PrerequisiteCourses.Select(p => new PrerequisiteEdgeRow
            {
                CourseId = c.CourseId,
                CourseCode = c.CourseCode,
                CourseName = c.CourseName,
                PrerequisiteCourseId = p.CourseId,
                PrerequisiteCourseCode = p.CourseCode,
                PrerequisiteCourseName = p.CourseName
            }))
            .OrderBy(x => x.CourseCode)
            .ThenBy(x => x.PrerequisiteCourseCode)
            .ToListAsync(ct);

        return rows;
    }

    public async Task<IReadOnlyList<PlanConstraintCheckRow>> GetPlanConstraintDataAsync(
        int studentId,
        int semesterId,
        IReadOnlyCollection<int> candidateCourseIds,
        CancellationToken ct = default)
    {
        var normalizedCandidateIds = candidateCourseIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (normalizedCandidateIds.Count == 0)
        {
            return [];
        }

        var candidateCourses = await _context.Courses
            .AsNoTracking()
            .Where(c => normalizedCandidateIds.Contains(c.CourseId))
            .Select(c => new
            {
                c.CourseId,
                c.CourseCode,
                c.CourseName,
                c.Credits,
                Prerequisites = c.PrerequisiteCourses.Select(p => new
                {
                    p.CourseId,
                    p.CourseCode,
                    p.CourseName
                }).ToList()
            })
            .ToListAsync(ct);

        var completedCourseIdList = await _context.Enrollments
            .AsNoTracking()
            .Where(e => e.StudentId == studentId && e.Status == "COMPLETED")
            .Select(e => e.CourseId)
            .Distinct()
            .ToListAsync(ct);

        var completedCourseIds = completedCourseIdList.ToHashSet();

        var existingEnrollments = await _context.Enrollments
            .AsNoTracking()
            .Include(e => e.Course)
            .Include(e => e.ClassSection)
                .ThenInclude(cs => cs.ScheduleEvents)
            .Where(e => e.StudentId == studentId
                && e.SemesterId == semesterId
                && e.Status == "ENROLLED")
            .ToListAsync(ct);

        var existingScheduleRows = existingEnrollments
            .SelectMany(e => e.ClassSection.ScheduleEvents.DefaultIfEmpty(), (e, se) => new
            {
                e.EnrollmentId,
                e.ClassSectionId,
                e.ClassSection.SectionCode,
                ExistingCourseId = e.CourseId,
                ExistingCourseCode = e.Course.CourseCode,
                ScheduleEventId = se?.ScheduleEventId,
                StartAt = se?.StartAt,
                EndAt = se?.EndAt,
                Status = se?.Status
            })
            .ToList();

        var rows = new List<PlanConstraintCheckRow>();

        foreach (var candidate in candidateCourses)
        {
            var prerequisiteRows = candidate.Prerequisites.Count == 0
                ? [new { CourseId = (int?)null, CourseCode = (string?)null, CourseName = (string?)null, IsCompleted = (bool?)null }]
                : candidate.Prerequisites
                    .Select(p => new
                    {
                        CourseId = (int?)p.CourseId,
                        CourseCode = (string?)p.CourseCode,
                        CourseName = (string?)p.CourseName,
                        IsCompleted = (bool?)completedCourseIds.Contains(p.CourseId)
                    })
                    .ToList();

            foreach (var pre in prerequisiteRows)
            {
                rows.Add(new PlanConstraintCheckRow
                {
                    CandidateCourseId = candidate.CourseId,
                    CandidateCourseCode = candidate.CourseCode,
                    CandidateCourseName = candidate.CourseName,
                    CandidateCredits = candidate.Credits,
                    PrerequisiteCourseId = pre.CourseId,
                    PrerequisiteCourseCode = pre.CourseCode,
                    PrerequisiteCourseName = pre.CourseName,
                    IsPrerequisiteCompleted = pre.IsCompleted
                });
            }

            if (existingScheduleRows.Count == 0)
            {
                continue;
            }

            foreach (var existing in existingScheduleRows)
            {
                rows.Add(new PlanConstraintCheckRow
                {
                    CandidateCourseId = candidate.CourseId,
                    CandidateCourseCode = candidate.CourseCode,
                    CandidateCourseName = candidate.CourseName,
                    CandidateCredits = candidate.Credits,
                    ExistingEnrollmentId = existing.EnrollmentId,
                    ExistingClassSectionId = existing.ClassSectionId,
                    ExistingSectionCode = existing.SectionCode,
                    ExistingCourseId = existing.ExistingCourseId,
                    ExistingCourseCode = existing.ExistingCourseCode,
                    ExistingScheduleEventId = existing.ScheduleEventId,
                    ExistingScheduleStartAt = existing.StartAt,
                    ExistingScheduleEndAt = existing.EndAt,
                    ExistingScheduleStatus = existing.Status
                });
            }
        }

        return rows
            .OrderBy(x => x.CandidateCourseCode)
            .ThenBy(x => x.PrerequisiteCourseCode)
            .ThenBy(x => x.ExistingCourseCode)
            .ThenBy(x => x.ExistingScheduleStartAt)
            .ToList();
    }
}
