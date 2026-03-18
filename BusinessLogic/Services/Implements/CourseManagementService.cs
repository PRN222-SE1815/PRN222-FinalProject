using System.Data;
using BusinessLogic.Constants;
using BusinessLogic.DTOs.Requests.CourseManagement;
using BusinessLogic.DTOs.Response;
using BusinessLogic.DTOs.Responses.CourseManagement;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Entities;
using BusinessObject.Enum;
using DataAccess;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessLogic.Services.Implements;

public sealed class CourseManagementService : ICourseManagementService
{
    private const string AdminRole = "ADMIN";
    private const string NotificationTypeCourseDeactivated = "COURSE_DEACTIVATED";
    private const string RealtimeEventCourseDeactivated = "CourseDeactivated";
    private const string RealtimeEventCourseCreated = "CourseCreated";
    private const string RealtimeEventCourseUpdated = "CourseUpdated";
    private const string StudentDeepLink = "/Student/MyCourses";
    private const string TeacherDeepLink = "/Teacher/MyClasses";

    private static readonly string[] ActiveStatusesToDrop =
    [
        EnrollmentStatus.ENROLLED.ToString(),
        EnrollmentStatus.PENDING_APPROVAL.ToString(),
        EnrollmentStatus.WAITLIST.ToString()
    ];

    private readonly ICourseRepository _courseRepository;
    private readonly IClassSectionRepository _classSectionRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly INotificationService _notificationService;
    private readonly IRealtimeEventDispatcher _realtimeEventDispatcher;
    private readonly IUserRepository _userRepository;
    private readonly SchoolManagementDbContext _dbContext;
    private readonly ILogger<CourseManagementService> _logger;

    public CourseManagementService(
        ICourseRepository courseRepository,
        IClassSectionRepository classSectionRepository,
        IEnrollmentRepository enrollmentRepository,
        INotificationService notificationService,
        IRealtimeEventDispatcher realtimeEventDispatcher,
        IUserRepository userRepository,
        SchoolManagementDbContext dbContext,
        ILogger<CourseManagementService> logger)
    {
        _courseRepository = courseRepository;
        _classSectionRepository = classSectionRepository;
        _enrollmentRepository = enrollmentRepository;
        _notificationService = notificationService;
        _realtimeEventDispatcher = realtimeEventDispatcher;
        _userRepository = userRepository;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ServiceResult<PagedResultDto>> GetCoursesAsync(
        int actorUserId,
        string actorRole,
        GetCoursesRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var authResult = await AuthorizeAdminAsync(actorUserId, actorRole);
            if (!authResult.IsSuccess)
            {
                return ServiceResult<PagedResultDto>.Fail(authResult.ErrorCode!, authResult.Message);
            }

            if (request is null)
            {
                _logger.LogWarning("GetCoursesAsync invalid input. ActorUserId={ActorUserId}", actorUserId);
                return ServiceResult<PagedResultDto>.Fail(ErrorCodes.InvalidInput, "Invalid request payload.");
            }

            var page = request.Page <= 0 ? 1 : request.Page;
            var pageSize = request.PageSize <= 0 ? 20 : Math.Clamp(request.PageSize, 10, 100);

            var (courses, totalCount) = await _courseRepository.GetPagedCoursesAsync(
                request.Keyword,
                request.IsActive,
                request.SemesterId,
                page,
                pageSize,
                ct);

            var mappedItems = new List<object>(courses.Count);
            foreach (var course in courses)
            {
                var sections = await _classSectionRepository.GetByCourseIdAsync(course.CourseId, false, ct);
                var semesterNames = sections
                    .Where(s => s.Semester is not null)
                    .Select(s => s.Semester.SemesterName)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                mappedItems.Add(new CourseListItemResponse
                {
                    CourseId = course.CourseId,
                    CourseCode = course.CourseCode,
                    CourseName = course.CourseName,
                    Credits = course.Credits,
                    IsActive = course.IsActive,
                    ClassSectionCount = sections.Count,
                    SemesterNames = semesterNames
                });
            }

            var result = new PagedResultDto
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                Items = mappedItems
            };

            return ServiceResult<PagedResultDto>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCoursesAsync failed. ActorUserId={ActorUserId}", actorUserId);
            return ServiceResult<PagedResultDto>.Fail(ErrorCodes.InternalError, "An unexpected error occurred.");
        }
    }

    public async Task<ServiceResult<CourseDetailResponse>> GetCourseDetailAsync(
        int actorUserId,
        string actorRole,
        int courseId,
        CancellationToken ct = default)
    {
        try
        {
            var authResult = await AuthorizeAdminAsync(actorUserId, actorRole);
            if (!authResult.IsSuccess)
            {
                return ServiceResult<CourseDetailResponse>.Fail(authResult.ErrorCode!, authResult.Message);
            }

            if (courseId <= 0)
            {
                _logger.LogWarning("GetCourseDetailAsync invalid courseId. CourseId={CourseId}", courseId);
                return ServiceResult<CourseDetailResponse>.Fail(ErrorCodes.InvalidInput, "CourseId must be greater than 0.");
            }

            var course = await _courseRepository.GetByIdAsync(courseId, ct);
            if (course is null)
            {
                return ServiceResult<CourseDetailResponse>.Fail(ErrorCodes.NotFound, "Course not found.");
            }

            return ServiceResult<CourseDetailResponse>.Success(MapCourseDetail(course));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCourseDetailAsync failed. ActorUserId={ActorUserId}, CourseId={CourseId}", actorUserId, courseId);
            return ServiceResult<CourseDetailResponse>.Fail(ErrorCodes.InternalError, "An unexpected error occurred.");
        }
    }

    public async Task<ServiceResult<CourseDetailResponse>> CreateCourseAsync(
        int actorUserId,
        string actorRole,
        CreateCourseRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var authResult = await AuthorizeAdminAsync(actorUserId, actorRole);
            if (!authResult.IsSuccess)
            {
                return ServiceResult<CourseDetailResponse>.Fail(authResult.ErrorCode!, authResult.Message);
            }

            var validation = ValidateCourseInput(request?.CourseCode, request?.CourseName, request?.Credits ?? 0);
            if (!validation.IsValid)
            {
                _logger.LogWarning("CreateCourseAsync invalid input. ActorUserId={ActorUserId}, Error={Error}", actorUserId, validation.Message);
                return ServiceResult<CourseDetailResponse>.Fail(ErrorCodes.InvalidInput, validation.Message);
            }

            var normalizedCode = request.CourseCode.Trim();
            var exists = await _courseRepository.ExistsByCodeAsync(normalizedCode, null, ct);
            if (exists)
            {
                return ServiceResult<CourseDetailResponse>.Fail(ErrorCodes.Conflict, "Course code already exists.");
            }

            var course = new Course
            {
                CourseCode = normalizedCode,
                CourseName = request.CourseName.Trim(),
                Credits = request.Credits,
                Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
                ContentHtml = string.IsNullOrWhiteSpace(request.ContentHtml) ? null : request.ContentHtml,
                LearningPathJson = string.IsNullOrWhiteSpace(request.LearningPathJson) ? null : request.LearningPathJson,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            await _courseRepository.AddAsync(course, ct);
            await _courseRepository.SaveChangesAsync(ct);

            // If a semester was selected, create an initial class section
            if (request.SemesterId.HasValue && request.SemesterId.Value > 0)
            {
                var semester = await _dbContext.Semesters
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.SemesterId == request.SemesterId.Value, ct);

                if (semester is not null)
                {
                    var firstTeacher = await _dbContext.Teachers
                        .AsNoTracking()
                        .OrderBy(t => t.TeacherId)
                        .FirstOrDefaultAsync(ct);

                    if (firstTeacher is not null)
                    {
                        var sectionCode = $"{normalizedCode}01";
                        var section = new ClassSection
                        {
                            CourseId = course.CourseId,
                            SemesterId = semester.SemesterId,
                            TeacherId = firstTeacher.TeacherId,
                            SectionCode = sectionCode,
                            IsOpen = false,
                            MaxCapacity = 30,
                            CurrentEnrollment = 0,
                            CreatedAt = DateTime.UtcNow
                        };

                        _dbContext.ClassSections.Add(section);
                        await _dbContext.SaveChangesAsync(ct);

                        _logger.LogInformation(
                            "Created initial ClassSection for new course. CourseId={CourseId}, SemesterId={SemesterId}, SectionCode={SectionCode}",
                            course.CourseId, semester.SemesterId, sectionCode);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "No teacher found to create initial ClassSection. CourseId={CourseId}, SemesterId={SemesterId}",
                            course.CourseId, request.SemesterId.Value);
                    }
                }
            }

            _logger.LogInformation(
                "CreateCourseAsync success. ActorUserId={ActorUserId}, CourseId={CourseId}, CourseCode={CourseCode}",
                actorUserId,
                course.CourseId,
                course.CourseCode);

            await _realtimeEventDispatcher.DispatchToAllAsync(
                RealtimeEventCourseCreated,
                new
                {
                    courseId = course.CourseId,
                    courseCode = course.CourseCode,
                    courseName = course.CourseName,
                    message = $"New course created: {course.CourseCode}"
                },
                ct);

            return ServiceResult<CourseDetailResponse>.Success(MapCourseDetail(course), "Course created successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CreateCourseAsync failed. ActorUserId={ActorUserId}", actorUserId);
            return ServiceResult<CourseDetailResponse>.Fail(ErrorCodes.InternalError, "An unexpected error occurred.");
        }
    }

    public async Task<ServiceResult<CourseDetailResponse>> UpdateCourseAsync(
        int actorUserId,
        string actorRole,
        UpdateCourseRequest request,
        CancellationToken ct = default)
    {
        try
        {
            var authResult = await AuthorizeAdminAsync(actorUserId, actorRole);
            if (!authResult.IsSuccess)
            {
                return ServiceResult<CourseDetailResponse>.Fail(authResult.ErrorCode!, authResult.Message);
            }

            if (request is null || request.CourseId <= 0)
            {
                _logger.LogWarning("UpdateCourseAsync invalid request. ActorUserId={ActorUserId}", actorUserId);
                return ServiceResult<CourseDetailResponse>.Fail(ErrorCodes.InvalidInput, "Invalid request payload.");
            }

            var validation = ValidateCourseInput(request.CourseCode, request.CourseName, request.Credits);
            if (!validation.IsValid)
            {
                return ServiceResult<CourseDetailResponse>.Fail(ErrorCodes.InvalidInput, validation.Message);
            }

            var course = await _courseRepository.GetForUpdateAsync(request.CourseId, ct);
            if (course is null)
            {
                return ServiceResult<CourseDetailResponse>.Fail(ErrorCodes.NotFound, "Course not found.");
            }

            var normalizedCode = request.CourseCode.Trim();
            var duplicateCode = await _courseRepository.ExistsByCodeAsync(normalizedCode, request.CourseId, ct);
            if (duplicateCode)
            {
                return ServiceResult<CourseDetailResponse>.Fail(ErrorCodes.Conflict, "Course code already exists.");
            }

            course.CourseCode = normalizedCode;
            course.CourseName = request.CourseName.Trim();
            course.Credits = request.Credits;
            course.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
            course.ContentHtml = string.IsNullOrWhiteSpace(request.ContentHtml) ? null : request.ContentHtml;
            course.LearningPathJson = string.IsNullOrWhiteSpace(request.LearningPathJson) ? null : request.LearningPathJson;
            course.IsActive = request.IsActive;

            _courseRepository.Update(course);
            await _courseRepository.SaveChangesAsync(ct);

            _logger.LogInformation(
                "UpdateCourseAsync success. ActorUserId={ActorUserId}, CourseId={CourseId}, CourseCode={CourseCode}",
                actorUserId,
                course.CourseId,
                course.CourseCode);

            await _realtimeEventDispatcher.DispatchToAllAsync(
                RealtimeEventCourseUpdated,
                new
                {
                    courseId = course.CourseId,
                    courseCode = course.CourseCode,
                    courseName = course.CourseName,
                    message = $"Course updated: {course.CourseCode}"
                },
                ct);

            return ServiceResult<CourseDetailResponse>.Success(MapCourseDetail(course), "Course updated successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpdateCourseAsync failed. ActorUserId={ActorUserId}, CourseId={CourseId}", actorUserId, request?.CourseId);
            return ServiceResult<CourseDetailResponse>.Fail(ErrorCodes.InternalError, "An unexpected error occurred.");
        }
    }

    public async Task<ServiceResult<DeactivateCourseResultResponse>> DeactivateCourseAsync(
        int actorUserId,
        string actorRole,
        DeactivateCourseRequest request,
        CancellationToken ct = default)
    {
        if (request is null || request.CourseId <= 0)
        {
            _logger.LogWarning("DeactivateCourseAsync invalid request. ActorUserId={ActorUserId}", actorUserId);
            return ServiceResult<DeactivateCourseResultResponse>.Fail(ErrorCodes.InvalidInput, "CourseId must be greater than 0.");
        }

        var authResult = await AuthorizeAdminAsync(actorUserId, actorRole);
        if (!authResult.IsSuccess)
        {
            return ServiceResult<DeactivateCourseResultResponse>.Fail(authResult.ErrorCode!, authResult.Message);
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            var now = DateTime.UtcNow;
            var reason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Course deactivated by admin"
                : request.Reason.Trim();

            var course = await _courseRepository.GetForUpdateAsync(request.CourseId, ct);
            if (course is null)
            {
                await transaction.RollbackAsync(ct);
                return ServiceResult<DeactivateCourseResultResponse>.Fail(ErrorCodes.NotFound, "Course not found.");
            }

            if (!course.IsActive)
            {
                await transaction.RollbackAsync(ct);
                _logger.LogWarning(
                    "DeactivateCourseAsync idempotent skip. ActorUserId={ActorUserId}, CourseId={CourseId}",
                    actorUserId,
                    request.CourseId);

                return ServiceResult<DeactivateCourseResultResponse>.Success(new DeactivateCourseResultResponse
                {
                    CourseId = course.CourseId,
                    ClosedSectionCount = 0,
                    DroppedEnrollmentCount = 0,
                    AffectedStudentCount = 0,
                    AffectedTeacherCount = 0,
                    Message = "Course already inactive"
                }, "Course already inactive");
            }

            course.IsActive = false;

            var sections = await _courseRepository.GetClassSectionsByCourseAsync(course.CourseId, true, ct);
            var closedSectionCount = 0;

            foreach (var section in sections)
            {
                if (section.IsOpen)
                {
                    section.IsOpen = false;
                    closedSectionCount++;
                }

                section.Notes = AppendDeactivateNote(section.Notes, reason, now);
            }

            var enrollments = await _enrollmentRepository.GetByCourseIdAndStatusesAsync(
                course.CourseId,
                ActiveStatusesToDrop,
                true,
                ct);

            var droppedEnrollmentCount = 0;
            if (request.DropActiveEnrollments)
            {
                foreach (var enrollment in enrollments)
                {
                    if (string.Equals(enrollment.Status, EnrollmentStatus.DROPPED.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    enrollment.Status = EnrollmentStatus.DROPPED.ToString();
                    enrollment.UpdatedAt = now;
                    droppedEnrollmentCount++;
                }
            }

            await _courseRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var studentUserIds = enrollments
                .Select(e => e.StudentId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            var teacherUserIds = sections
                .Select(s => s.TeacherId)
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            await SendCourseDeactivatedNotificationsAsync(course, reason, studentUserIds, teacherUserIds, ct);

            var realtimePayload = new
            {
                courseId = course.CourseId,
                courseCode = course.CourseCode,
                courseName = course.CourseName,
                closedSectionCount,
                droppedEnrollmentCount,
                reason
            };

            await _realtimeEventDispatcher.DispatchToAllAsync(
                RealtimeEventCourseDeactivated,
                realtimePayload,
                ct);

            _logger.LogInformation(
                "DeactivateCourseAsync success. ActorUserId={ActorUserId}, CourseId={CourseId}, ClosedSections={ClosedSectionCount}, DroppedEnrollments={DroppedEnrollmentCount}",
                actorUserId,
                course.CourseId,
                closedSectionCount,
                droppedEnrollmentCount);

            return ServiceResult<DeactivateCourseResultResponse>.Success(new DeactivateCourseResultResponse
            {
                CourseId = course.CourseId,
                ClosedSectionCount = closedSectionCount,
                DroppedEnrollmentCount = droppedEnrollmentCount,
                AffectedStudentCount = studentUserIds.Count,
                AffectedTeacherCount = teacherUserIds.Count,
                Message = "Course deactivated successfully."
            }, "Course deactivated successfully.");
        }
        catch (Exception ex)
        {
            try
            {
                await transaction.RollbackAsync(ct);
            }
            catch (Exception rollbackEx)
            {
                _logger.LogError(rollbackEx, "DeactivateCourseAsync rollback failed. CourseId={CourseId}", request.CourseId);
            }

            _logger.LogError(
                ex,
                "DeactivateCourseAsync failed. ActorUserId={ActorUserId}, CourseId={CourseId}",
                actorUserId,
                request.CourseId);

            return ServiceResult<DeactivateCourseResultResponse>.Fail(ErrorCodes.InternalError, "An unexpected error occurred.");
        }
    }

    private async Task<ServiceResult<bool>> AuthorizeAdminAsync(int actorUserId, string actorRole)
    {
        if (!string.Equals(actorRole, AdminRole, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("AuthorizeAdminAsync forbidden by actorRole. ActorUserId={ActorUserId}, ActorRole={ActorRole}", actorUserId, actorRole);
            return ServiceResult<bool>.Fail(ErrorCodes.Forbidden, "Only admin is allowed.");
        }

        var actor = await _userRepository.GetUserByIdAsync(actorUserId);
        if (actor is null || !actor.IsActive || !string.Equals(actor.Role, UserRole.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("AuthorizeAdminAsync forbidden by actor state. ActorUserId={ActorUserId}", actorUserId);
            return ServiceResult<bool>.Fail(ErrorCodes.Forbidden, "Only active admin is allowed.");
        }

        return ServiceResult<bool>.Success(true);
    }

    private static (bool IsValid, string Message) ValidateCourseInput(string? courseCode, string? courseName, int credits)
    {
        if (string.IsNullOrWhiteSpace(courseCode))
        {
            return (false, "Course code is required.");
        }

        if (string.IsNullOrWhiteSpace(courseName))
        {
            return (false, "Course name is required.");
        }

        if (credits < 1 || credits > 10)
        {
            return (false, "Credits must be in range 1..10.");
        }

        return (true, string.Empty);
    }

    private static CourseDetailResponse MapCourseDetail(Course course)
    {
        return new CourseDetailResponse
        {
            CourseId = course.CourseId,
            CourseCode = course.CourseCode,
            CourseName = course.CourseName,
            Credits = course.Credits,
            Description = course.Description,
            ContentHtml = course.ContentHtml,
            LearningPathJson = course.LearningPathJson,
            IsActive = course.IsActive
        };
    }

    private static string AppendDeactivateNote(string? existingNotes, string reason, DateTime nowUtc)
    {
        var noteLine = $"[Course deactivated {nowUtc:yyyy-MM-dd HH:mm:ss} UTC] {reason}";
        if (string.IsNullOrWhiteSpace(existingNotes))
        {
            return noteLine;
        }

        return $"{existingNotes.Trim()}\n{noteLine}";
    }

    private async Task SendCourseDeactivatedNotificationsAsync(
        Course course,
        string reason,
        IReadOnlyCollection<int> studentUserIds,
        IReadOnlyCollection<int> teacherUserIds,
        CancellationToken ct)
    {
        var title = $"Course deactivated: {course.CourseCode}";
        var message = $"Course {course.CourseCode} - {course.CourseName} has been deactivated. Reason: {reason}.";

        if (studentUserIds.Count > 0)
        {
            var studentNotifyResult = await _notificationService.SendAsync(
                NotificationTypeCourseDeactivated,
                title,
                message,
                StudentDeepLink,
                studentUserIds,
                ct);

            if (!studentNotifyResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Student notification failed for course deactivation. CourseId={CourseId}, ErrorCode={ErrorCode}",
                    course.CourseId,
                    studentNotifyResult.ErrorCode);
            }
        }

        if (teacherUserIds.Count > 0)
        {
            var teacherNotifyResult = await _notificationService.SendAsync(
                NotificationTypeCourseDeactivated,
                title,
                message,
                TeacherDeepLink,
                teacherUserIds,
                ct);

            if (!teacherNotifyResult.IsSuccess)
            {
                _logger.LogWarning(
                    "Teacher notification failed for course deactivation. CourseId={CourseId}, ErrorCode={ErrorCode}",
                    course.CourseId,
                    teacherNotifyResult.ErrorCode);
            }
        }
    }

    public async Task<ServiceResult<IReadOnlyList<SemesterOptionDto>>> GetAllSemesterOptionsAsync(
        int actorUserId,
        string actorRole,
        CancellationToken ct = default)
    {
        try
        {
            var authResult = await AuthorizeAdminAsync(actorUserId, actorRole);
            if (!authResult.IsSuccess)
            {
                return ServiceResult<IReadOnlyList<SemesterOptionDto>>.Fail(authResult.ErrorCode!, authResult.Message);
            }

            var semesters = await _dbContext.Semesters
                .AsNoTracking()
                .OrderByDescending(s => s.StartDate)
                .Select(s => new SemesterOptionDto
                {
                    SemesterId = s.SemesterId,
                    SemesterName = s.SemesterName,
                    IsActive = s.IsActive,
                    StartDate = s.StartDate
                })
                .ToListAsync(ct);

            return ServiceResult<IReadOnlyList<SemesterOptionDto>>.Success(semesters);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetAllSemesterOptionsAsync failed. ActorUserId={ActorUserId}", actorUserId);
            return ServiceResult<IReadOnlyList<SemesterOptionDto>>.Fail(ErrorCodes.InternalError, "An unexpected error occurred.");
        }
    }

    public async Task<ServiceResult<IReadOnlyList<CourseSectionDto>>> GetCourseSectionsAsync(
        int actorUserId,
        string actorRole,
        int courseId,
        CancellationToken ct = default)
    {
        try
        {
            var authResult = await AuthorizeAdminAsync(actorUserId, actorRole);
            if (!authResult.IsSuccess)
            {
                return ServiceResult<IReadOnlyList<CourseSectionDto>>.Fail(authResult.ErrorCode!, authResult.Message);
            }

            var sections = await _classSectionRepository.GetByCourseIdAsync(courseId, false, ct);
            var result = sections.Select(s => new CourseSectionDto
            {
                ClassSectionId = s.ClassSectionId,
                SectionCode = s.SectionCode,
                SemesterName = s.Semester?.SemesterName ?? "—",
                CurrentEnrollment = s.CurrentEnrollment,
                MaxCapacity = s.MaxCapacity,
                IsOpen = s.IsOpen,
                Room = s.Room ?? "—"
            }).ToList();

            return ServiceResult<IReadOnlyList<CourseSectionDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetCourseSectionsAsync failed. CourseId={CourseId}", courseId);
            return ServiceResult<IReadOnlyList<CourseSectionDto>>.Fail(ErrorCodes.InternalError, "Failed to load sections.");
        }
    }
}
