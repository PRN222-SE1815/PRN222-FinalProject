using System.Data;
using BusinessLogic.DTOs.GradeAppeals;
using BusinessLogic.DTOs.Response;
using BusinessLogic.DTOs.Responses;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Entities;
using BusinessObject.Enum;
using DataAccess;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using BlGradeAppealDetailDto = BusinessLogic.DTOs.GradeAppeals.GradeAppealDetailDto;
using BlGradeAppealListItemDto = BusinessLogic.DTOs.GradeAppeals.GradeAppealListItemDto;
using BlGradeAppealQueryRequest = BusinessLogic.DTOs.GradeAppeals.GradeAppealQueryRequest;
using BlPagedResult = BusinessLogic.DTOs.Response.PagedResult<BusinessLogic.DTOs.GradeAppeals.GradeAppealListItemDto>;
using GradeAppealDetailRow = DataAccess.Repositories.Models.GradeAppealDetailDto;
using GradeAppealListItemRow = DataAccess.Repositories.Models.GradeAppealListItemDto;

namespace BusinessLogic.Services.Implements;

public sealed class GradeAppealService : IGradeAppealService
{
    private const int MaxAppealContentLength = 1000;
    private const int MaxEvidenceNoteLength = 500;
    private const int MaxResponseMessageLength = 1000;

    private readonly IGradeAppealRepository _gradeAppealRepository;
    private readonly IGradeBookRepository _gradeBookRepository;
    private readonly IUserRepository _userRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IClassSectionRepository _classSectionRepository;
    private readonly ISemesterRepository _semesterRepository;
    private readonly SchoolManagementDbContext _context;
    private readonly ILogger<GradeAppealService> _logger;

    public GradeAppealService(
        IGradeAppealRepository gradeAppealRepository,
        IGradeBookRepository gradeBookRepository,
        IUserRepository userRepository,
        IStudentRepository studentRepository,
        IClassSectionRepository classSectionRepository,
        ISemesterRepository semesterRepository,
        SchoolManagementDbContext context,
        ILogger<GradeAppealService> logger)
    {
        _gradeAppealRepository = gradeAppealRepository;
        _gradeBookRepository = gradeBookRepository;
        _userRepository = userRepository;
        _studentRepository = studentRepository;
        _classSectionRepository = classSectionRepository;
        _semesterRepository = semesterRepository;
        _context = context;
        _logger = logger;
    }

    public async Task<IReadOnlyList<SemesterOptionDto>> GetSemesterOptionsAsync(CancellationToken ct = default)
    {
        var semesters = await _semesterRepository.GetAllSemestersAsync();
        return semesters
            .OrderByDescending(s => s.StartDate)
            .Select(s => new SemesterOptionDto
            {
                SemesterId = s.SemesterId,
                SemesterCode = s.SemesterCode,
                SemesterName = s.SemesterName,
                IsActive = s.IsActive,
                StartDate = s.StartDate,
                EndDate = s.EndDate
            })
            .ToList();
    }

    public async Task<ServiceResult<BlGradeAppealDetailDto>> SubmitAppealAsync(int userId, SubmitGradeAppealRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("INVALID_REQUEST", "Request is required.");
        }

        var actor = await _userRepository.GetUserByIdAsync(userId);
        if (actor == null || !actor.IsActive || !string.Equals(actor.Role, UserRole.STUDENT.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("FORBIDDEN", "Only students can submit grade appeals.");
        }

        var student = await _studentRepository.GetStudentByUserIdAsync(userId);
        if (student == null)
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("STUDENT_NOT_FOUND", "Student profile not found.");
        }

        var appealContent = request.AppealContent?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(appealContent) || appealContent.Length > MaxAppealContentLength)
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("INVALID_APPEAL_CONTENT", "Appeal content is required and must not exceed 1000 characters.");
        }

        var evidenceNote = string.IsNullOrWhiteSpace(request.EvidenceNote) ? null : request.EvidenceNote.Trim();
        if (evidenceNote is { Length: > MaxEvidenceNoteLength })
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("INVALID_EVIDENCE_NOTE", "Evidence note must not exceed 500 characters.");
        }

        var enrollment = await _gradeAppealRepository.GetEnrollmentByIdAsync(request.EnrollmentId, ct);
        if (enrollment == null)
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("ENROLLMENT_NOT_FOUND", "Enrollment not found.");
        }

        if (enrollment.StudentId != student.StudentId)
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("FORBIDDEN", "You can submit appeals only for your own enrollment.");
        }

        var gradeBook = await _gradeAppealRepository.GetGradeBookByIdAsync(request.GradeBookId, ct);
        if (gradeBook == null)
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("GRADEBOOK_NOT_FOUND", "Gradebook not found.");
        }

        if (gradeBook.ClassSectionId != enrollment.ClassSectionId)
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("GRADEBOOK_ENROLLMENT_MISMATCH", "Gradebook does not match the enrollment class section.");
        }

        if (!string.Equals(gradeBook.Status, GradeBookStatus.PUBLISHED.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("GRADEBOOK_NOT_PUBLISHED", "Grade appeal is allowed only after gradebook is published.");
        }

        if (request.GradeItemId.HasValue)
        {
            var gradeItemExists = await _context.GradeItems
                .AsNoTracking()
                .AnyAsync(x => x.GradeItemId == request.GradeItemId.Value && x.GradeBookId == request.GradeBookId, ct);

            if (!gradeItemExists)
            {
                return ServiceResult<BlGradeAppealDetailDto>.Fail("GRADE_ITEM_NOT_FOUND", "Grade item does not belong to this gradebook.");
            }
        }

        var hasDuplicate = await _gradeAppealRepository.ExistsDuplicateAsync(request.GradeBookId, student.StudentId, ct);
        if (hasDuplicate)
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("DUPLICATE_APPEAL", "An appeal already exists for this gradebook.");
        }

        var now = DateTime.UtcNow;
        var entity = new GradeAppeal
        {
            GradeBookId = request.GradeBookId,
            EnrollmentId = request.EnrollmentId,
            StudentId = student.StudentId,
            GradeItemId = request.GradeItemId,
            AppealContent = appealContent,
            EvidenceNote = evidenceNote,
            Status = GradeAppealStatus.Submitted,
            SubmittedAt = now
        };

        try
        {
            await _gradeAppealRepository.AddAsync(entity, ct);
            await _gradeAppealRepository.SaveChangesAsync(ct);

            var created = await _gradeAppealRepository.GetByIdAsync(entity.AppealId, ct);
            if (created == null)
            {
                return ServiceResult<BlGradeAppealDetailDto>.Fail("APPEAL_NOT_FOUND", "Unable to load created appeal.");
            }

            return ServiceResult<BlGradeAppealDetailDto>.Success(MapDetail(created), "Appeal submitted successfully.");
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "SubmitAppealAsync DB update conflict. UserId={UserId}, GradeBookId={GradeBookId}", userId, request.GradeBookId);
            return ServiceResult<BlGradeAppealDetailDto>.Fail("DUPLICATE_APPEAL", "An appeal already exists for this gradebook.");
        }
    }

    public async Task<ServiceResult<BlGradeAppealDetailDto>> StartReviewAsync(int reviewerUserId, long appealId, CancellationToken ct = default)
    {
        var auth = await AuthorizeReviewerAsync(reviewerUserId, appealId, ct);
        if (!auth.IsSuccess)
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail(auth.ErrorCode!, auth.Message);
        }

        var appeal = await _gradeAppealRepository.GetTrackedByIdAsync(appealId, ct);
        if (appeal == null)
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("APPEAL_NOT_FOUND", "Appeal not found.");
        }

        if (!string.Equals(appeal.Status, GradeAppealStatus.Submitted, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("INVALID_STATUS", "Only submitted appeals can be moved to review.");
        }

        appeal.Status = GradeAppealStatus.UnderReview;
        await _gradeAppealRepository.UpdateAsync(appeal, ct);
        await _gradeAppealRepository.SaveChangesAsync(ct);

        var updated = await _gradeAppealRepository.GetByIdAsync(appeal.AppealId, ct);
        if (updated == null)
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("APPEAL_NOT_FOUND", "Appeal not found after update.");
        }

        return ServiceResult<BlGradeAppealDetailDto>.Success(MapDetail(updated), "Appeal moved to under review.");
    }

    public async Task<ServiceResult<BlGradeAppealDetailDto>> ResolveAppealAsync(int reviewerUserId, ResolveGradeAppealRequest request, CancellationToken ct = default)
    {
        if (request is null)
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("INVALID_REQUEST", "Request is required.");
        }

        var outcome = request.Outcome?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!string.Equals(outcome, GradeAppealStatus.Approved, StringComparison.Ordinal)
            && !string.Equals(outcome, GradeAppealStatus.Rejected, StringComparison.Ordinal))
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("INVALID_OUTCOME", "Outcome must be APPROVED or REJECTED.");
        }

        var responseMessage = request.ResponseMessage?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(responseMessage) || responseMessage.Length > MaxResponseMessageLength)
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("INVALID_RESPONSE_MESSAGE", "Response message is required and must not exceed 1000 characters.");
        }

        var auth = await AuthorizeReviewerAsync(reviewerUserId, request.AppealId, ct);
        if (!auth.IsSuccess)
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail(auth.ErrorCode!, auth.Message);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            var appeal = await _gradeAppealRepository.GetTrackedByIdAsync(request.AppealId, ct);
            if (appeal == null)
            {
                await transaction.RollbackAsync(ct);
                return ServiceResult<BlGradeAppealDetailDto>.Fail("APPEAL_NOT_FOUND", "Appeal not found.");
            }

            if (!string.Equals(appeal.Status, GradeAppealStatus.UnderReview, StringComparison.OrdinalIgnoreCase))
            {
                await transaction.RollbackAsync(ct);
                return ServiceResult<BlGradeAppealDetailDto>.Fail("INVALID_STATUS", "Only UNDER_REVIEW appeals can be resolved.");
            }

            if (string.Equals(outcome, GradeAppealStatus.Rejected, StringComparison.Ordinal)
                && (request.GradeEntryId.HasValue || request.NewScore.HasValue))
            {
                await transaction.RollbackAsync(ct);
                return ServiceResult<BlGradeAppealDetailDto>.Fail("INVALID_SCORE_CHANGE", "Score change is allowed only for approved appeals.");
            }

            if (request.GradeEntryId.HasValue || request.NewScore.HasValue)
            {
                if (!request.GradeEntryId.HasValue || !request.NewScore.HasValue)
                {
                    await transaction.RollbackAsync(ct);
                    return ServiceResult<BlGradeAppealDetailDto>.Fail("INVALID_SCORE_CHANGE", "Both GradeEntryId and NewScore are required for score change.");
                }

                var gradeEntry = await _gradeAppealRepository.GetGradeEntryByIdAsync(request.GradeEntryId.Value, ct);
                if (gradeEntry == null)
                {
                    await transaction.RollbackAsync(ct);
                    return ServiceResult<BlGradeAppealDetailDto>.Fail("GRADE_ENTRY_NOT_FOUND", "Grade entry not found.");
                }

                if (gradeEntry.EnrollmentId != appeal.EnrollmentId || gradeEntry.GradeItem.GradeBookId != appeal.GradeBookId)
                {
                    await transaction.RollbackAsync(ct);
                    return ServiceResult<BlGradeAppealDetailDto>.Fail("GRADE_ENTRY_MISMATCH", "Grade entry does not belong to this appeal context.");
                }

                if (appeal.GradeItemId.HasValue && gradeEntry.GradeItemId != appeal.GradeItemId.Value)
                {
                    await transaction.RollbackAsync(ct);
                    return ServiceResult<BlGradeAppealDetailDto>.Fail("GRADE_ENTRY_MISMATCH", "Grade entry item does not match the appealed grade item.");
                }

                if (request.NewScore.Value < 0 || request.NewScore.Value > gradeEntry.GradeItem.MaxScore)
                {
                    await transaction.RollbackAsync(ct);
                    return ServiceResult<BlGradeAppealDetailDto>.Fail("INVALID_SCORE", "New score is out of valid range.");
                }

                var oldScore = gradeEntry.Score;
                if (oldScore != request.NewScore.Value)
                {
                    gradeEntry.Score = request.NewScore.Value;
                    gradeEntry.UpdatedBy = reviewerUserId;
                    gradeEntry.UpdatedAt = DateTime.UtcNow;

                    var auditLog = new GradeAuditLog
                    {
                        GradeEntryId = gradeEntry.GradeEntryId,
                        ActorUserId = reviewerUserId,
                        OldScore = oldScore,
                        NewScore = request.NewScore.Value,
                        Reason = responseMessage,
                        CreatedAt = DateTime.UtcNow
                    };

                    await _gradeBookRepository.AddGradeAuditLogAsync(auditLog, ct);
                }
            }

            appeal.Status = outcome;
            appeal.ResponseMessage = responseMessage;
            appeal.ResolvedBy = reviewerUserId;
            appeal.ResolvedAt = DateTime.UtcNow;

            await _gradeAppealRepository.UpdateAsync(appeal, ct);
            await _gradeAppealRepository.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            var updated = await _gradeAppealRepository.GetByIdAsync(appeal.AppealId, ct);
            if (updated == null)
            {
                return ServiceResult<BlGradeAppealDetailDto>.Fail("APPEAL_NOT_FOUND", "Appeal not found after resolve.");
            }

            return ServiceResult<BlGradeAppealDetailDto>.Success(MapDetail(updated), "Appeal resolved successfully.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            _logger.LogError(ex, "ResolveAppealAsync failed. AppealId={AppealId}, ReviewerUserId={ReviewerUserId}", request.AppealId, reviewerUserId);
            return ServiceResult<BlGradeAppealDetailDto>.Fail("SYSTEM_ERROR", "Unexpected error occurred while resolving appeal.");
        }
    }

    public async Task<ServiceResult<BlGradeAppealDetailDto>> GetDetailAsync(int userId, long appealId, CancellationToken ct = default)
    {
        var actor = await _userRepository.GetUserByIdAsync(userId);
        if (actor == null || !actor.IsActive)
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("UNAUTHORIZED", "User not found or inactive.");
        }

        var detail = await _gradeAppealRepository.GetByIdAsync(appealId, ct);
        if (detail == null)
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("APPEAL_NOT_FOUND", "Appeal not found.");
        }

        var canView = await CanAccessAppealAsync(actor, detail.StudentId, detail.ClassSectionId, ct);
        if (!canView)
        {
            return ServiceResult<BlGradeAppealDetailDto>.Fail("FORBIDDEN", "You do not have permission to view this appeal.");
        }

        return ServiceResult<BlGradeAppealDetailDto>.Success(MapDetail(detail));
    }

    public async Task<ServiceResult<BlPagedResult>> GetPagedAsync(int userId, BlGradeAppealQueryRequest query, CancellationToken ct = default)
    {
        var actor = await _userRepository.GetUserByIdAsync(userId);
        if (actor == null || !actor.IsActive)
        {
            return ServiceResult<BlPagedResult>.Fail("UNAUTHORIZED", "User not found or inactive.");
        }

        var model = query ?? new BlGradeAppealQueryRequest();

        var repositoryRequest = new DataAccess.Repositories.Models.GradeAppealQueryRequest
        {
            StudentId = model.StudentId,
            SemesterId = model.SemesterId,
            GradeBookId = model.GradeBookId,
            EnrollmentId = model.EnrollmentId,
            Status = model.Status,
            SubmittedFrom = model.SubmittedFrom,
            SubmittedTo = model.SubmittedTo,
            ResolvedBy = model.ResolvedBy,
            Page = model.Page,
            PageSize = model.PageSize
        };

        if (string.Equals(actor.Role, UserRole.STUDENT.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            var student = await _studentRepository.GetStudentByUserIdAsync(userId);
            if (student == null)
            {
                return ServiceResult<BlPagedResult>.Fail("STUDENT_NOT_FOUND", "Student profile not found.");
            }

            repositoryRequest.StudentId = student.StudentId;
            repositoryRequest.TeacherUserId = null;
        }
        else if (string.Equals(actor.Role, UserRole.TEACHER.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            repositoryRequest.TeacherUserId = userId;
            repositoryRequest.StudentId = model.StudentId;
        }
        else if (!string.Equals(actor.Role, UserRole.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<BlPagedResult>.Fail("FORBIDDEN", "Role is not allowed to query appeals.");
        }

        var paged = await _gradeAppealRepository.GetPagedAsync(repositoryRequest, ct);

        var mapped = new BlPagedResult
        {
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            Items = paged.Items.Select(MapListItem).ToList()
        };

        return ServiceResult<BlPagedResult>.Success(mapped);
    }

    private async Task<ServiceResult<bool>> AuthorizeReviewerAsync(int reviewerUserId, long appealId, CancellationToken ct)
    {
        var reviewer = await _userRepository.GetUserByIdAsync(reviewerUserId);
        if (reviewer == null || !reviewer.IsActive)
        {
            return ServiceResult<bool>.Fail("UNAUTHORIZED", "Reviewer not found or inactive.");
        }

        if (string.Equals(reviewer.Role, UserRole.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<bool>.Success(true);
        }

        if (!string.Equals(reviewer.Role, UserRole.TEACHER.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<bool>.Fail("FORBIDDEN", "Only admin or teacher can review appeals.");
        }

        var detail = await _gradeAppealRepository.GetByIdAsync(appealId, ct);
        if (detail == null)
        {
            return ServiceResult<bool>.Fail("APPEAL_NOT_FOUND", "Appeal not found.");
        }

        var assigned = await _classSectionRepository.IsTeacherAssignedAsync(reviewerUserId, detail.ClassSectionId);
        if (!assigned)
        {
            return ServiceResult<bool>.Fail("FORBIDDEN", "Teacher is not assigned to the related class section.");
        }

        return ServiceResult<bool>.Success(true);
    }

    private async Task<bool> CanAccessAppealAsync(User actor, int appealStudentId, int classSectionId, CancellationToken ct)
    {
        if (string.Equals(actor.Role, UserRole.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(actor.Role, UserRole.STUDENT.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            var student = await _studentRepository.GetStudentByUserIdAsync(actor.UserId);
            return student != null && student.StudentId == appealStudentId;
        }

        if (string.Equals(actor.Role, UserRole.TEACHER.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return await _classSectionRepository.IsTeacherAssignedAsync(actor.UserId, classSectionId);
        }

        return false;
    }

    private static BlGradeAppealDetailDto MapDetail(GradeAppealDetailRow source)
    {
        return new BlGradeAppealDetailDto
        {
            AppealId = source.AppealId,
            GradeBookId = source.GradeBookId,
            ClassSectionId = source.ClassSectionId,
            EnrollmentId = source.EnrollmentId,
            StudentId = source.StudentId,
            GradeItemId = source.GradeItemId,
            AppealContent = source.AppealContent,
            EvidenceNote = source.EvidenceNote,
            Status = source.Status,
            ResponseMessage = source.ResponseMessage,
            ResolvedBy = source.ResolvedBy,
            SubmittedAt = source.SubmittedAt,
            ResolvedAt = source.ResolvedAt,
            StudentCode = source.StudentCode,
            StudentFullName = source.StudentFullName,
            GradeBookStatus = source.GradeBookStatus,
            ClassSectionCode = source.ClassSectionCode,
            CourseCode = source.CourseCode,
            CourseName = source.CourseName,
            GradeItemName = source.GradeItemName,
            GradeItemScore = source.GradeItemScore,
            GradeItemMaxScore = source.GradeItemMaxScore,
            ResolvedByFullName = source.ResolvedByFullName
        };
    }

    private static BlGradeAppealListItemDto MapListItem(GradeAppealListItemRow source)
    {
        return new BlGradeAppealListItemDto
        {
            AppealId = source.AppealId,
            GradeBookId = source.GradeBookId,
            ClassSectionId = source.ClassSectionId,
            EnrollmentId = source.EnrollmentId,
            StudentId = source.StudentId,
            GradeItemId = source.GradeItemId,
            AppealContent = source.AppealContent,
            EvidenceNote = source.EvidenceNote,
            Status = source.Status,
            SubmittedAt = source.SubmittedAt,
            ResolvedAt = source.ResolvedAt,
            ResolvedBy = source.ResolvedBy,
            ResponseMessage = source.ResponseMessage,
            StudentCode = source.StudentCode,
            StudentFullName = source.StudentFullName,
            GradeBookStatus = source.GradeBookStatus,
            ClassSectionCode = source.ClassSectionCode,
            CourseCode = source.CourseCode,
            CourseName = source.CourseName,
            SemesterId = source.SemesterId,
            SemesterCode = source.SemesterCode,
            SemesterName = source.SemesterName,
            ResolvedByFullName = source.ResolvedByFullName
        };
    }
}
