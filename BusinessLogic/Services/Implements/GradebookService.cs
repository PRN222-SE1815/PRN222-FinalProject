using BusinessLogic.Constants;
using BusinessLogic.DTOs.Requests.Gradebook;
using BusinessLogic.DTOs.Response;
using BusinessLogic.DTOs.Responses.Gradebook;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Entities;
using DataAccess;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessLogic.Services.Implements;

public sealed class GradebookService : IGradebookService
{
    private const string RoleTeacher = "TEACHER";
    private const string RoleAdmin = "ADMIN";
    private const string RoleStudent = "STUDENT";

    private const string StatusDraft = "DRAFT";
    private const string StatusPendingApproval = "PENDING_APPROVAL";
    private const string StatusRejected = "REJECTED";
    private const string StatusPublished = "PUBLISHED";
    private const string StatusLocked = "LOCKED";
    private const string StatusArchived = "ARCHIVED";

    private readonly IGradeBookRepository _gradeBookRepository;
    private readonly IClassSectionRepository _classSectionRepository;
    private readonly IUserRepository _userRepository;
    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly INotificationService _notificationService;
    private readonly SchoolManagementDbContext _dbContext;
    private readonly ILogger<GradebookService> _logger;

    public GradebookService(
        IGradeBookRepository gradeBookRepository,
        IClassSectionRepository classSectionRepository,
        IUserRepository userRepository,
        IEnrollmentRepository enrollmentRepository,
        INotificationService notificationService,
        SchoolManagementDbContext dbContext,
        ILogger<GradebookService> logger)
    {
        _gradeBookRepository = gradeBookRepository;
        _classSectionRepository = classSectionRepository;
        _userRepository = userRepository;
        _enrollmentRepository = enrollmentRepository;
        _notificationService = notificationService;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ServiceResult<IReadOnlyList<TeacherClassSectionDto>>> GetTeacherClassSectionsAsync(
        int teacherUserId,
        string actorRole,
        CancellationToken ct = default)
    {
        try
        {
            if (!string.Equals(actorRole, RoleTeacher, StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<IReadOnlyList<TeacherClassSectionDto>>.Fail(ErrorCodes.Forbidden, "Only teachers can view their class sections.");
            }

            var user = await _userRepository.GetUserByIdAsync(teacherUserId);
            if (user is null || !string.Equals(user.Role, RoleTeacher, StringComparison.OrdinalIgnoreCase) || !user.IsActive)
            {
                return ServiceResult<IReadOnlyList<TeacherClassSectionDto>>.Fail(ErrorCodes.Forbidden, "Actor is not an active teacher.");
            }

            var sections = await _classSectionRepository.GetByTeacherIdAsync(teacherUserId, ct);

            var result = sections.Select(cs => new TeacherClassSectionDto
            {
                ClassSectionId = cs.ClassSectionId,
                SectionCode = cs.SectionCode,
                CourseCode = cs.Course.CourseCode,
                CourseName = cs.Course.CourseName,
                Credits = cs.Course.Credits,
                SemesterId = cs.SemesterId,
                SemesterName = cs.Semester.SemesterName,
                IsOpen = cs.IsOpen,
                MaxCapacity = cs.MaxCapacity,
                CurrentEnrollment = cs.CurrentEnrollment,
                Room = cs.Room,
                GradebookStatus = cs.GradeBook?.Status
            }).ToList();

            return ServiceResult<IReadOnlyList<TeacherClassSectionDto>>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTeacherClassSectionsAsync failed. TeacherUserId={TeacherUserId}", teacherUserId);
            return ServiceResult<IReadOnlyList<TeacherClassSectionDto>>.Fail(ErrorCodes.InternalError, "An unexpected error occurred.");
        }
    }

    public async Task<ServiceResult<GradebookDetailResponse>> GetGradebookAsync(
        int actorUserId,
        string actorRole,
        int classSectionId,
        CancellationToken ct = default)
    {
        try
        {
            if (classSectionId <= 0)
            {
                return ServiceResult<GradebookDetailResponse>.Fail(ErrorCodes.InvalidInput, "ClassSectionId must be greater than 0.");
            }

            var authorization = await AuthorizeReadAsync(actorUserId, actorRole, classSectionId);
            if (!authorization.IsSuccess)
            {
                return ServiceResult<GradebookDetailResponse>.Fail(authorization.ErrorCode!, authorization.Message);
            }

            var gradebook = await _gradeBookRepository.GetDetailByClassSectionIdAsync(classSectionId, ct);
            if (gradebook is null)
            {
                return ServiceResult<GradebookDetailResponse>.Fail(ErrorCodes.GradebookNotFound, "Gradebook not found.");
            }

            var cs = gradebook.ClassSection;
            var response = new GradebookDetailResponse
            {
                GradeBookId = gradebook.GradeBookId,
                ClassSectionId = gradebook.ClassSectionId,
                SectionCode = cs?.SectionCode ?? string.Empty,
                CourseCode = cs?.Course?.CourseCode ?? string.Empty,
                CourseName = cs?.Course?.CourseName ?? string.Empty,
                SemesterName = cs?.Semester?.SemesterName ?? string.Empty,
                Status = gradebook.Status,
                Version = gradebook.Version,
                GradeItems = gradebook.GradeItems
                    .OrderBy(x => x.SortOrder)
                    .ThenBy(x => x.GradeItemId)
                    .Select(x => new GradeItemResponse
                    {
                        GradeItemId = x.GradeItemId,
                        ItemName = x.ItemName,
                        MaxScore = x.MaxScore,
                        Weight = x.Weight,
                        IsRequired = x.IsRequired,
                        SortOrder = x.SortOrder
                    })
                    .ToList(),
                GradeEntries = gradebook.GradeItems
                    .SelectMany(x => x.GradeEntries)
                    .OrderBy(x => x.GradeItemId)
                    .ThenBy(x => x.EnrollmentId)
                    .Select(x => new GradeEntryResponse
                    {
                        GradeEntryId = x.GradeEntryId,
                        GradeItemId = x.GradeItemId,
                        EnrollmentId = x.EnrollmentId,
                        StudentCode = x.Enrollment?.Student?.StudentCode ?? string.Empty,
                        StudentName = x.Enrollment?.Student?.StudentNavigation?.FullName ?? string.Empty,
                        Score = x.Score,
                        UpdatedAt = x.UpdatedAt
                    })
                    .ToList()
            };

            return ServiceResult<GradebookDetailResponse>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetGradebookAsync failed. ActorUserId={ActorUserId}, ClassSectionId={ClassSectionId}", actorUserId, classSectionId);
            return ServiceResult<GradebookDetailResponse>.Fail(ErrorCodes.InternalError, "An unexpected error occurred.");
        }
    }

    public async Task<ServiceResult<bool>> UpsertTeacherScoresAsync(
        int teacherUserId,
        string actorRole,
        UpsertScoresRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (request is null || request.ClassSectionId <= 0)
            {
                return ServiceResult<bool>.Fail(ErrorCodes.InvalidInput, "Invalid request payload.");
            }

            var authorization = await AuthorizeTeacherOwnershipAsync(teacherUserId, actorRole, request.ClassSectionId);
            if (!authorization.IsSuccess)
            {
                return ServiceResult<bool>.Fail(authorization.ErrorCode!, authorization.Message);
            }

            var gradebook = await _gradeBookRepository.GetDetailByClassSectionIdAsync(request.ClassSectionId, ct);
            if (gradebook is null)
            {
                return ServiceResult<bool>.Fail(ErrorCodes.GradebookNotFound, "Gradebook not found.");
            }

            if (!IsEditableStatus(gradebook.Status))
            {
                return ServiceResult<bool>.Fail(ErrorCodes.InvalidState, $"Gradebook is not editable in status '{gradebook.Status}'.");
            }

            var scoreCells = request.Scores ?? [];
            if (scoreCells.Count == 0)
            {
                return ServiceResult<bool>.Fail(ErrorCodes.InvalidInput, "Scores payload is required.");
            }

            var gradeItemIds = gradebook.GradeItems.Select(x => x.GradeItemId).ToHashSet();
            var enrollmentIds = await GetEnrollmentIdsForGradebookAsync(request.ClassSectionId, ct);
            var auditCount = 0;

            foreach (var cell in scoreCells)
            {
                if (cell.GradeItemId <= 0 || cell.EnrollmentId <= 0)
                {
                    return ServiceResult<bool>.Fail(ErrorCodes.InvalidInput, "GradeItemId and EnrollmentId must be greater than 0.");
                }

                if (!gradeItemIds.Contains(cell.GradeItemId))
                {
                    return ServiceResult<bool>.Fail(ErrorCodes.ItemNotFound, $"Grade item {cell.GradeItemId} does not belong to this gradebook.");
                }

                if (!enrollmentIds.Contains(cell.EnrollmentId))
                {
                    return ServiceResult<bool>.Fail(ErrorCodes.InvalidInput, $"Enrollment {cell.EnrollmentId} is not available for this gradebook.");
                }

                if (cell.Score.HasValue && (cell.Score.Value < 0m || cell.Score.Value > 10m))
                {
                    return ServiceResult<bool>.Fail(ErrorCodes.InvalidInput, "Score must be in range 0..10.");
                }

                var oldEntry = await _gradeBookRepository.GetGradeEntryAsync(cell.GradeItemId, cell.EnrollmentId, ct);

                var isChanged = oldEntry is null
                    ? cell.Score.HasValue
                    : oldEntry.Score != cell.Score;

                if (!isChanged)
                {
                    continue;
                }

                var upsertEntry = new GradeEntry
                {
                    GradeItemId = cell.GradeItemId,
                    EnrollmentId = cell.EnrollmentId,
                    Score = cell.Score,
                    UpdatedBy = teacherUserId,
                    UpdatedAt = DateTime.UtcNow
                };

                await _gradeBookRepository.UpsertGradeEntryAsync(upsertEntry, ct);

                var auditLog = new GradeAuditLog
                {
                    ActorUserId = teacherUserId,
                    OldScore = oldEntry?.Score,
                    NewScore = cell.Score,
                    Reason = string.IsNullOrWhiteSpace(cell.Reason) ? "TEACHER_MANUAL_EDIT" : cell.Reason.Trim(),
                    CreatedAt = DateTime.UtcNow
                };

                if (oldEntry is null)
                {
                    auditLog.GradeEntry = upsertEntry;
                }
                else
                {
                    auditLog.GradeEntryId = oldEntry.GradeEntryId;
                }

                await _gradeBookRepository.AddGradeAuditLogAsync(auditLog, ct);
                auditCount++;
            }

            await _gradeBookRepository.SaveChangesAsync(ct);

            _logger.LogInformation(
                "UpsertTeacherScores completed. Action=UPSERT_SCORES, UserId={UserId}, ClassSectionId={ClassSectionId}, GradeBookId={GradeBookId}, AuditEntries={AuditCount}",
                teacherUserId, request.ClassSectionId, gradebook.GradeBookId, auditCount);

            return ServiceResult<bool>.Success(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "UpsertTeacherScoresAsync failed. TeacherUserId={TeacherUserId}, ClassSectionId={ClassSectionId}", teacherUserId, request?.ClassSectionId);
            return ServiceResult<bool>.Fail(ErrorCodes.InternalError, "An unexpected error occurred.");
        }
    }

    public async Task<ServiceResult<GradebookApprovalResponse>> RequestApprovalAsync(
        int teacherUserId,
        string actorRole,
        RequestApprovalRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (request is null || request.ClassSectionId <= 0)
            {
                return ServiceResult<GradebookApprovalResponse>.Fail(ErrorCodes.InvalidInput, "Invalid request payload.");
            }

            var authorization = await AuthorizeTeacherOwnershipAsync(teacherUserId, actorRole, request.ClassSectionId);
            if (!authorization.IsSuccess)
            {
                return ServiceResult<GradebookApprovalResponse>.Fail(authorization.ErrorCode!, authorization.Message);
            }

            var gradebook = await _gradeBookRepository.GetByClassSectionIdAsync(request.ClassSectionId, ct);
            if (gradebook is null)
            {
                return ServiceResult<GradebookApprovalResponse>.Fail(ErrorCodes.GradebookNotFound, "Gradebook not found.");
            }

            if (!string.Equals(gradebook.Status, StatusDraft, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(gradebook.Status, StatusRejected, StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<GradebookApprovalResponse>.Fail(ErrorCodes.InvalidState,
                    $"Gradebook cannot transition from '{gradebook.Status}' to '{StatusPendingApproval}'.");
            }

            var statusFrom = gradebook.Status;
            var now = DateTime.UtcNow;
            gradebook.Status = StatusPendingApproval;
            gradebook.UpdatedAt = now;
            gradebook.Version += 1;

            _gradeBookRepository.UpdateGradeBook(gradebook);

            var approval = new GradeBookApproval
            {
                GradeBookId = gradebook.GradeBookId,
                RequestBy = teacherUserId,
                RequestAt = now,
                RequestMessage = string.IsNullOrWhiteSpace(request.RequestMessage) ? null : request.RequestMessage.Trim()
            };

            await _gradeBookRepository.AddGradeBookApprovalAsync(approval, ct);
            await _gradeBookRepository.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Gradebook state changed. Action=REQUEST_APPROVAL, UserId={UserId}, ClassSectionId={ClassSectionId}, GradeBookId={GradeBookId}, StatusFrom={StatusFrom}, StatusTo={StatusTo}",
                teacherUserId, request.ClassSectionId, gradebook.GradeBookId, statusFrom, StatusPendingApproval);

            await SendGradebookNotificationSafeAsync(
                "GRADEBOOK_REVIEW_REQUESTED",
                "Gradebook Review Requested",
                $"Teacher has submitted gradebook for class section #{request.ClassSectionId} for review.",
                $"/Admin/GradebookManagement/Review/{request.ClassSectionId}",
                request.ClassSectionId,
                NotifyTarget.Admins,
                ct);

            return ServiceResult<GradebookApprovalResponse>.Success(new GradebookApprovalResponse
            {
                ApprovalId = approval.ApprovalId,
                GradeBookId = approval.GradeBookId,
                Outcome = approval.Outcome,
                RequestAt = approval.RequestAt,
                ResponseAt = approval.ResponseAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RequestApprovalAsync failed. TeacherUserId={TeacherUserId}, ClassSectionId={ClassSectionId}", teacherUserId, request?.ClassSectionId);
            return ServiceResult<GradebookApprovalResponse>.Fail(ErrorCodes.InternalError, "An unexpected error occurred.");
        }
    }

    public async Task<ServiceResult<GradebookApprovalResponse>> ApproveGradebookAsync(
        int adminUserId,
        string actorRole,
        ApproveGradebookRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (request is null || request.ClassSectionId <= 0)
            {
                return ServiceResult<GradebookApprovalResponse>.Fail(ErrorCodes.InvalidInput, "Invalid request payload.");
            }

            var authorization = await AuthorizeAdminAsync(adminUserId, actorRole);
            if (!authorization.IsSuccess)
            {
                return ServiceResult<GradebookApprovalResponse>.Fail(authorization.ErrorCode!, authorization.Message);
            }

            var gradebook = await _gradeBookRepository.GetByClassSectionIdAsync(request.ClassSectionId, ct);
            if (gradebook is null)
            {
                return ServiceResult<GradebookApprovalResponse>.Fail(ErrorCodes.GradebookNotFound, "Gradebook not found.");
            }

            if (!string.Equals(gradebook.Status, StatusPendingApproval, StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<GradebookApprovalResponse>.Fail(ErrorCodes.InvalidState,
                    $"Gradebook cannot transition from '{gradebook.Status}' to '{StatusPublished}'.");
            }

            var approval = await _dbContext.GradeBookApprovals
                .Where(x => x.GradeBookId == gradebook.GradeBookId)
                .OrderByDescending(x => x.RequestAt ?? DateTime.MinValue)
                .ThenByDescending(x => x.ApprovalId)
                .FirstOrDefaultAsync(ct);

            if (approval is null)
            {
                return ServiceResult<GradebookApprovalResponse>.Fail(ErrorCodes.InvalidState, "No approval request found.");
            }

            var statusFrom = gradebook.Status;
            var now = DateTime.UtcNow;
            gradebook.Status = StatusPublished;
            gradebook.PublishedAt = now;
            gradebook.UpdatedAt = now;
            gradebook.Version += 1;
            _gradeBookRepository.UpdateGradeBook(gradebook);

            approval.Outcome = "APPROVED";
            approval.ResponseBy = adminUserId;
            approval.ResponseAt = now;
            approval.ResponseMessage = string.IsNullOrWhiteSpace(request.ResponseMessage) ? null : request.ResponseMessage.Trim();

            await _gradeBookRepository.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Gradebook state changed. Action=APPROVE, UserId={UserId}, ClassSectionId={ClassSectionId}, GradeBookId={GradeBookId}, StatusFrom={StatusFrom}, StatusTo={StatusTo}",
                adminUserId, request.ClassSectionId, gradebook.GradeBookId, statusFrom, StatusPublished);

            await SendGradebookNotificationSafeAsync(
                "GRADE_PUBLISHED",
                "Gradebook Published",
                $"Grades for class section #{request.ClassSectionId} have been published.",
                "/Student/StudentGrade/Index",
                request.ClassSectionId,
                NotifyTarget.TeacherAndStudents,
                ct);

            return ServiceResult<GradebookApprovalResponse>.Success(new GradebookApprovalResponse
            {
                ApprovalId = approval.ApprovalId,
                GradeBookId = approval.GradeBookId,
                Outcome = approval.Outcome,
                RequestAt = approval.RequestAt,
                ResponseAt = approval.ResponseAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ApproveGradebookAsync failed. AdminUserId={AdminUserId}, ClassSectionId={ClassSectionId}", adminUserId, request?.ClassSectionId);
            return ServiceResult<GradebookApprovalResponse>.Fail(ErrorCodes.InternalError, "An unexpected error occurred.");
        }
    }

    public async Task<ServiceResult<GradebookApprovalResponse>> RejectGradebookAsync(
        int adminUserId,
        string actorRole,
        RejectGradebookRequest request,
        CancellationToken ct = default)
    {
        try
        {
            if (request is null || request.ClassSectionId <= 0 || string.IsNullOrWhiteSpace(request.ResponseMessage))
            {
                return ServiceResult<GradebookApprovalResponse>.Fail(ErrorCodes.InvalidInput, "ClassSectionId and response message are required.");
            }

            var authorization = await AuthorizeAdminAsync(adminUserId, actorRole);
            if (!authorization.IsSuccess)
            {
                return ServiceResult<GradebookApprovalResponse>.Fail(authorization.ErrorCode!, authorization.Message);
            }

            var gradebook = await _gradeBookRepository.GetByClassSectionIdAsync(request.ClassSectionId, ct);
            if (gradebook is null)
            {
                return ServiceResult<GradebookApprovalResponse>.Fail(ErrorCodes.GradebookNotFound, "Gradebook not found.");
            }

            if (!string.Equals(gradebook.Status, StatusPendingApproval, StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<GradebookApprovalResponse>.Fail(ErrorCodes.InvalidState,
                    $"Gradebook cannot transition from '{gradebook.Status}' to '{StatusRejected}'.");
            }

            var approval = await _dbContext.GradeBookApprovals
                .Where(x => x.GradeBookId == gradebook.GradeBookId)
                .OrderByDescending(x => x.RequestAt ?? DateTime.MinValue)
                .ThenByDescending(x => x.ApprovalId)
                .FirstOrDefaultAsync(ct);

            if (approval is null)
            {
                return ServiceResult<GradebookApprovalResponse>.Fail(ErrorCodes.InvalidState, "No approval request found.");
            }

            var statusFrom = gradebook.Status;
            var now = DateTime.UtcNow;
            gradebook.Status = StatusRejected;
            gradebook.UpdatedAt = now;
            gradebook.Version += 1;
            _gradeBookRepository.UpdateGradeBook(gradebook);

            approval.Outcome = "REJECTED";
            approval.ResponseBy = adminUserId;
            approval.ResponseAt = now;
            approval.ResponseMessage = request.ResponseMessage.Trim();

            await _gradeBookRepository.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Gradebook state changed. Action=REJECT, UserId={UserId}, ClassSectionId={ClassSectionId}, GradeBookId={GradeBookId}, StatusFrom={StatusFrom}, StatusTo={StatusTo}",
                adminUserId, request.ClassSectionId, gradebook.GradeBookId, statusFrom, StatusRejected);

            await SendGradebookNotificationSafeAsync(
                "GRADEBOOK_REJECTED",
                "Gradebook Rejected",
                $"Gradebook for class section #{request.ClassSectionId} was rejected. Reason: {request.ResponseMessage.Trim()}",
                $"/Teacher/TeacherGrade/Index?ClassSectionId={request.ClassSectionId}",
                request.ClassSectionId,
                NotifyTarget.Teacher,
                ct);

            return ServiceResult<GradebookApprovalResponse>.Success(new GradebookApprovalResponse
            {
                ApprovalId = approval.ApprovalId,
                GradeBookId = approval.GradeBookId,
                Outcome = approval.Outcome,
                RequestAt = approval.RequestAt,
                ResponseAt = approval.ResponseAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RejectGradebookAsync failed. AdminUserId={AdminUserId}, ClassSectionId={ClassSectionId}", adminUserId, request?.ClassSectionId);
            return ServiceResult<GradebookApprovalResponse>.Fail(ErrorCodes.InternalError, "An unexpected error occurred.");
        }
    }

    private async Task<ServiceResult<bool>> AuthorizeReadAsync(int actorUserId, string actorRole, int classSectionId)
    {
        if (string.Equals(actorRole, RoleAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return await AuthorizeAdminAsync(actorUserId, actorRole);
        }

        if (string.Equals(actorRole, RoleTeacher, StringComparison.OrdinalIgnoreCase))
        {
            return await AuthorizeTeacherOwnershipAsync(actorUserId, actorRole, classSectionId);
        }

        if (string.Equals(actorRole, RoleStudent, StringComparison.OrdinalIgnoreCase))
        {
            return await AuthorizeStudentEnrolledAsync(actorUserId, actorRole, classSectionId);
        }

        return ServiceResult<bool>.Fail(ErrorCodes.Forbidden, "Actor role is not allowed.");
    }

    private async Task<ServiceResult<bool>> AuthorizeAdminAsync(int adminUserId, string actorRole)
    {
        if (!string.Equals(actorRole, RoleAdmin, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<bool>.Fail(ErrorCodes.Forbidden, "Only admin can perform this action.");
        }

        var user = await _userRepository.GetUserByIdAsync(adminUserId);
        if (user is null || !string.Equals(user.Role, RoleAdmin, StringComparison.OrdinalIgnoreCase) || !user.IsActive)
        {
            return ServiceResult<bool>.Fail(ErrorCodes.Forbidden, "Actor is not an active admin.");
        }

        return ServiceResult<bool>.Success(true);
    }

    private async Task<ServiceResult<bool>> AuthorizeTeacherOwnershipAsync(int teacherUserId, string actorRole, int classSectionId)
    {
        if (!string.Equals(actorRole, RoleTeacher, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<bool>.Fail(ErrorCodes.Forbidden, "Only teacher can perform this action.");
        }

        var user = await _userRepository.GetUserByIdAsync(teacherUserId);
        if (user is null || !string.Equals(user.Role, RoleTeacher, StringComparison.OrdinalIgnoreCase) || !user.IsActive)
        {
            return ServiceResult<bool>.Fail(ErrorCodes.Forbidden, "Actor is not an active teacher.");
        }

        var isAssigned = await _classSectionRepository.IsTeacherAssignedAsync(teacherUserId, classSectionId);
        if (!isAssigned)
        {
            return ServiceResult<bool>.Fail(ErrorCodes.Forbidden, "Teacher is not assigned to this class section.");
        }

        return ServiceResult<bool>.Success(true);
    }

    private async Task<ServiceResult<bool>> AuthorizeStudentEnrolledAsync(int studentUserId, string actorRole, int classSectionId)
    {
        if (!string.Equals(actorRole, RoleStudent, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<bool>.Fail(ErrorCodes.Forbidden, "Only student can perform this action.");
        }

        var isEnrolled = await _enrollmentRepository.IsStudentEnrolledAsync(studentUserId, classSectionId);
        if (!isEnrolled)
        {
            return ServiceResult<bool>.Fail(ErrorCodes.Forbidden, "Student is not enrolled in this class section.");
        }

        return ServiceResult<bool>.Success(true);
    }

    private async Task<HashSet<int>> GetEnrollmentIdsForGradebookAsync(int classSectionId, CancellationToken ct)
    {
        const int pageSize = 200;
        var page = 1;
        var collected = new List<Enrollment>();

        while (true)
        {
            var result = await _gradeBookRepository.GetEnrollmentsForGradeBookAsync(classSectionId, page, pageSize, ct);
            if (result.Items.Count == 0)
            {
                break;
            }

            collected.AddRange(result.Items);

            if (collected.Count >= result.TotalCount)
            {
                break;
            }

            page++;
        }

        return collected.Select(x => x.EnrollmentId).ToHashSet();
    }

    // ==================== Notification Helpers ====================

    private enum NotifyTarget { Admins, Teacher, TeacherAndStudents }

    private async Task SendGradebookNotificationSafeAsync(
        string notificationType,
        string title,
        string message,
        string deepLink,
        int classSectionId,
        NotifyTarget target,
        CancellationToken ct)
    {
        try
        {
            var recipients = new List<int>();

            switch (target)
            {
                case NotifyTarget.Admins:
                    var adminIds = await _userRepository.GetActiveUserIdsByRoleAsync(RoleAdmin, ct);
                    recipients.AddRange(adminIds);
                    break;

                case NotifyTarget.Teacher:
                {
                    var cs = await _classSectionRepository.GetClassSectionWithCourseAsync(classSectionId);
                    if (cs is not null && cs.TeacherId > 0)
                        recipients.Add(cs.TeacherId);
                    break;
                }

                case NotifyTarget.TeacherAndStudents:
                {
                    var cs2 = await _classSectionRepository.GetClassSectionWithCourseAsync(classSectionId);
                    if (cs2 is not null && cs2.TeacherId > 0)
                        recipients.Add(cs2.TeacherId);

                    var studentIds = await _enrollmentRepository.GetEnrolledStudentUserIdsAsync(classSectionId, ct);
                    recipients.AddRange(studentIds);
                    break;
                }
            }

            if (recipients.Count == 0)
            {
                return;
            }

            await _notificationService.SendAsync(notificationType, title, message, deepLink, recipients, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to send {NotificationType} notification for ClassSectionId={ClassSectionId}",
                notificationType, classSectionId);
        }
    }

    // ==================== State Helpers ====================

    private static bool IsEditableStatus(string status)
    {
        return string.Equals(status, StatusDraft, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, StatusRejected, StringComparison.OrdinalIgnoreCase);
    }

    // ==================== Admin Gradebook Listing ====================

    public async Task<ServiceResult<(IReadOnlyList<GradeBook> Items, int TotalCount)>> GetPagedGradebooksForAdminAsync(
        int adminUserId,
        string actorRole,
        string? statusFilter,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        try
        {
            if (!string.Equals(actorRole, RoleAdmin, StringComparison.OrdinalIgnoreCase))
            {
                return ServiceResult<(IReadOnlyList<GradeBook>, int)>.Fail(ErrorCodes.Forbidden, "Only admin can perform this action.");
            }

            var result = await _gradeBookRepository.GetPagedGradebooksAsync(statusFilter, page, pageSize, ct);
            return ServiceResult<(IReadOnlyList<GradeBook>, int)>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPagedGradebooksForAdminAsync failed. AdminUserId={AdminUserId}", adminUserId);
            return ServiceResult<(IReadOnlyList<GradeBook>, int)>.Fail(ErrorCodes.InternalError, "An unexpected error occurred.");
        }
    }

    public async Task<ServiceResult<int?>> GetActiveSemesterIdAsync(CancellationToken ct = default)
    {
        try
        {
            var active = await _dbContext.Semesters
                .AsNoTracking()
                .Where(s => s.IsActive)
                .Select(s => (int?)s.SemesterId)
                .FirstOrDefaultAsync(ct);

            return ServiceResult<int?>.Success(active);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetActiveSemesterIdAsync failed.");
            return ServiceResult<int?>.Fail(ErrorCodes.InternalError, "An unexpected error occurred.");
        }
    }
}
