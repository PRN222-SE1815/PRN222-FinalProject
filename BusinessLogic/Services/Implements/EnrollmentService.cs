using System.Data;
using BusinessLogic.DTOs.Response;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Entities;
using BusinessObject.Enum;
using DataAccess;
using DataAccess.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BusinessLogic.Services.Implements;

public sealed class EnrollmentService : IEnrollmentService
{
    private const decimal FallbackAmountPerCredit = 0m;
    private const decimal FallbackSurcharge = 0m;
    private static readonly string[] ActiveDuplicateStatuses =
    {
        EnrollmentStatus.ENROLLED.ToString(),
        EnrollmentStatus.PENDING_APPROVAL.ToString(),
        EnrollmentStatus.WAITLIST.ToString()
    };

    private static readonly string[] ActiveScheduleStatuses =
    {
        EnrollmentStatus.ENROLLED.ToString(),
        EnrollmentStatus.PENDING_APPROVAL.ToString()
    };

    private readonly IEnrollmentRepository _enrollmentRepository;
    private readonly IClassSectionRepository _classSectionRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IWalletRepository _walletRepository;
    private readonly ISemesterTuitionPolicyRepository _semesterTuitionPolicyRepository;
    private readonly ICourseRepository _courseRepository;
    private readonly IUserRepository _userRepository;
    private readonly ISemesterRepository _semesterRepository;
    private readonly SchoolManagementDbContext _context;
    private readonly ILogger<EnrollmentService> _logger;

    public EnrollmentService(
        IEnrollmentRepository enrollmentRepository,
        IClassSectionRepository classSectionRepository,
        IStudentRepository studentRepository,
        IWalletRepository walletRepository,
        ISemesterTuitionPolicyRepository semesterTuitionPolicyRepository,
        ICourseRepository courseRepository,
        IUserRepository userRepository,
        ISemesterRepository semesterRepository,
        SchoolManagementDbContext context,
        ILogger<EnrollmentService> logger)
    {
        _enrollmentRepository = enrollmentRepository;
        _classSectionRepository = classSectionRepository;
        _studentRepository = studentRepository;
        _walletRepository = walletRepository;
        _semesterTuitionPolicyRepository = semesterTuitionPolicyRepository;
        _courseRepository = courseRepository;
        _userRepository = userRepository;
        _semesterRepository = semesterRepository;
        _context = context;
        _logger = logger;
    }

    public async Task<ServiceResult<EnrollmentResponse>> RegisterAndPayAsync(int userId, int classSectionId)
    {
        _logger.LogInformation("RegisterAndPayAsync started — UserId={UserId}, ClassSectionId={ClassSectionId}", userId, classSectionId);

        var user = await _userRepository.GetUserByIdAsync(userId);
        if (user == null || !string.Equals(user.Role, UserRole.STUDENT.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("RegisterAndPayAsync UNAUTHORIZED — UserId={UserId}", userId);
            return ServiceResult<EnrollmentResponse>.Fail("UNAUTHORIZED", "Tài khoản không có quyền đăng ký học phần.");
        }

        var student = await _studentRepository.GetStudentByUserIdAsync(userId);
        if (student == null)
        {
            _logger.LogError("RegisterAndPayAsync STUDENT_NOT_FOUND — UserId={UserId}", userId);
            return ServiceResult<EnrollmentResponse>.Fail("STUDENT_NOT_FOUND", "Không tìm thấy sinh viên.");
        }

        var classSectionInfo = await _classSectionRepository.GetClassSectionWithCourseAsync(classSectionId);
        if (classSectionInfo == null)
        {
            _logger.LogError("RegisterAndPayAsync CLASSSECTION_NOT_FOUND — ClassSectionId={ClassSectionId}", classSectionId);
            return ServiceResult<EnrollmentResponse>.Fail("CLASSSECTION_NOT_FOUND", "Không tìm thấy lớp học phần.");
        }

        var semester = classSectionInfo.Semester;
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (semester.RegistrationEndDate.HasValue && today > semester.RegistrationEndDate.Value)
        {
            _logger.LogWarning("RegisterAndPayAsync SEMESTER_WINDOW_CLOSED (RegistrationEndDate) — UserId={UserId}, ClassSectionId={ClassSectionId}, SemesterId={SemesterId}", userId, classSectionId, semester.SemesterId);
            return ServiceResult<EnrollmentResponse>.Fail("SEMESTER_WINDOW_CLOSED", "Đã hết hạn đăng ký học phần.");
        }

        if (semester.AddDropDeadline.HasValue && today > semester.AddDropDeadline.Value)
        {
            _logger.LogWarning("RegisterAndPayAsync SEMESTER_WINDOW_CLOSED (AddDropDeadline) — UserId={UserId}, ClassSectionId={ClassSectionId}, SemesterId={SemesterId}", userId, classSectionId, semester.SemesterId);
            return ServiceResult<EnrollmentResponse>.Fail("SEMESTER_WINDOW_CLOSED", "Đã hết hạn add/drop học phần.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var classSection = await _classSectionRepository.GetClassSectionForUpdateAsync(classSectionId);
            if (classSection == null)
            {
                await transaction.RollbackAsync();
                _logger.LogError("RegisterAndPayAsync ClassSection null after lock — ClassSectionId={ClassSectionId}", classSectionId);
                return ServiceResult<EnrollmentResponse>.Fail("CLASSSECTION_NOT_FOUND", "Không tìm thấy lớp học phần.");
            }

            var wallet = await _walletRepository.GetWalletForUpdateAsync(student.StudentId);
            if (wallet == null)
            {
                await transaction.RollbackAsync();
                _logger.LogError("RegisterAndPayAsync Wallet not found — StudentId={StudentId}", student.StudentId);
                return ServiceResult<EnrollmentResponse>.Fail("WALLET_INSUFFICIENT", "Ví sinh viên không tồn tại.");
            }

            var hasDuplicate = await _context.Enrollments
                .AsNoTracking()
                .AnyAsync(e => e.StudentId == student.StudentId
                    && e.SemesterId == classSectionInfo.SemesterId
                    && e.CourseId == classSectionInfo.CourseId
                    && ActiveDuplicateStatuses.Contains(e.Status));

            if (hasDuplicate)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning("RegisterAndPayAsync DUPLICATE_ENROLLMENT — UserId={UserId}, CourseId={CourseId}, SemesterId={SemesterId}", userId, classSectionInfo.CourseId, classSectionInfo.SemesterId);
                return ServiceResult<EnrollmentResponse>.Fail("DUPLICATE_ENROLLMENT", "Bạn đã đăng ký môn học này trong học kỳ.");
            }

            var prerequisiteOk = await _courseRepository.CheckPrerequisiteSatisfiedAsync(student.StudentId, classSectionInfo.CourseId);
            if (!prerequisiteOk)
            {
                await transaction.RollbackAsync();

                var missingCourses = await _courseRepository.GetMissingPrerequisiteCoursesAsync(student.StudentId, classSectionInfo.CourseId);
                var missingList = missingCourses.Select(c => new PrerequisiteInfoDto
                {
                    CourseId = c.CourseId,
                    CourseCode = c.CourseCode,
                    CourseName = c.CourseName
                }).ToList();

                _logger.LogWarning("RegisterAndPayAsync PREREQ_NOT_MET — UserId={UserId}, CourseId={CourseId}, MissingCount={MissingCount}", userId, classSectionInfo.CourseId, missingList.Count);

                var failResponse = new EnrollmentResponse
                {
                    ClassSectionId = classSectionId,
                    CourseId = classSectionInfo.CourseId,
                    Message = "Bạn chưa đủ điều kiện tiên quyết.",
                    MissingPrerequisites = missingList
                };
                return ServiceResult<EnrollmentResponse>.Fail("PREREQ_NOT_MET", "Bạn chưa đủ điều kiện tiên quyết.", failResponse);
            }

            var currentCredits = await _enrollmentRepository.GetCurrentCreditsAsync(student.StudentId, classSectionInfo.SemesterId);
            if (currentCredits + classSectionInfo.Course.Credits > semester.MaxCredits)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning("RegisterAndPayAsync CREDIT_LIMIT_EXCEEDED — UserId={UserId}, CurrentCredits={CurrentCredits}, CourseCredits={CourseCredits}, MaxCredits={MaxCredits}", userId, currentCredits, classSectionInfo.Course.Credits, semester.MaxCredits);
                return ServiceResult<EnrollmentResponse>.Fail("CREDIT_LIMIT_EXCEEDED", "Vượt quá giới hạn tín chỉ cho phép.");
            }

            var hasTimeConflict = await HasTimeConflictAsync(student.StudentId, classSectionInfo.SemesterId, classSectionId);
            if (hasTimeConflict)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning("RegisterAndPayAsync TIME_CONFLICT — UserId={UserId}, ClassSectionId={ClassSectionId}", userId, classSectionId);
                return ServiceResult<EnrollmentResponse>.Fail("TIME_CONFLICT", "Lịch học bị trùng với lớp khác.");
            }

            if (!classSection.IsOpen || classSection.CurrentEnrollment >= classSection.MaxCapacity)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning("RegisterAndPayAsync CLASS_FULL — ClassSectionId={ClassSectionId}, CurrentEnrollment={CurrentEnrollment}, MaxCapacity={MaxCapacity}", classSectionId, classSection.CurrentEnrollment, classSection.MaxCapacity);
                return ServiceResult<EnrollmentResponse>.Fail("CLASS_FULL", "Lớp học phần đã đủ chỗ.");
            }

            var semesterTuitionPolicy = await _semesterTuitionPolicyRepository
                .GetActivePolicyBySemesterAsync(classSectionInfo.SemesterId, DateTime.UtcNow);
            if (semesterTuitionPolicy == null)
            {
                await transaction.RollbackAsync();
                _logger.LogError("RegisterAndPayAsync TUITION_POLICY_NOT_FOUND — SemesterId={SemesterId}", classSectionInfo.SemesterId);
                return ServiceResult<EnrollmentResponse>.Fail("TUITION_POLICY_NOT_FOUND", "Không tìm thấy chính sách học phí hiệu lực cho học kỳ.");
            }

            var feeAmount = (classSectionInfo.Course.Credits * semesterTuitionPolicy.AmountPerCredit)
                + semesterTuitionPolicy.DefaultSurcharge;

            if (wallet.Balance < feeAmount)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning("RegisterAndPayAsync WALLET_INSUFFICIENT — UserId={UserId}, ClassSectionId={ClassSectionId}", userId, classSectionId);
                return ServiceResult<EnrollmentResponse>.Fail("WALLET_INSUFFICIENT", "Số dư ví không đủ để thanh toán.");
            }

            wallet.Balance -= feeAmount;
            wallet.LastUpdated = DateTime.UtcNow;

            classSection.CurrentEnrollment += 1;

            var enrollment = new Enrollment
            {
                StudentId = student.StudentId,
                ClassSectionId = classSectionId,
                SemesterId = classSectionInfo.SemesterId,
                CourseId = classSectionInfo.CourseId,
                CreditsSnapshot = classSectionInfo.Course.Credits,
                Status = EnrollmentStatus.PENDING_APPROVAL.ToString(),
                EnrolledAt = DateTime.UtcNow
            };

            var walletTransaction = new WalletTransaction
            {
                WalletId = wallet.WalletId,
                Amount = -feeAmount,
                TransactionType = "CHECKOUT_PAYMENT",
                Description = "Thanh toán học phí đăng ký học phần",
                CreatedAt = DateTime.UtcNow
            };

            await _walletRepository.AddWalletTransactionAsync(walletTransaction);
            await _enrollmentRepository.AddEnrollmentAsync(enrollment);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation("RegisterAndPayAsync SUCCESS — EnrollmentId={EnrollmentId}, StudentId={StudentId}, ClassSectionId={ClassSectionId}", enrollment.EnrollmentId, student.StudentId, classSectionId);
            return ServiceResult<EnrollmentResponse>.Success(MapEnrollmentResponse(enrollment, feeAmount, wallet.Balance, "Đăng ký thành công, chờ phê duyệt."));
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "RegisterAndPayAsync DB EXCEPTION — UserId={UserId}, ClassSectionId={ClassSectionId}", userId, classSectionId);

            if (ex.InnerException?.Message.Contains("UX_Enrollments_Student_Course_Sem_Active", StringComparison.OrdinalIgnoreCase) == true)
            {
                return ServiceResult<EnrollmentResponse>.Fail("DUPLICATE_ENROLLMENT", "Bạn đã có đăng ký đang hoạt động cho môn học này trong học kỳ.");
            }

            return ServiceResult<EnrollmentResponse>.Fail("SYSTEM_ERROR", "Có lỗi hệ thống, vui lòng thử lại.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "RegisterAndPayAsync EXCEPTION — UserId={UserId}, ClassSectionId={ClassSectionId}", userId, classSectionId);
            return ServiceResult<EnrollmentResponse>.Fail("SYSTEM_ERROR", "Có lỗi hệ thống, vui lòng thử lại.");
        }
    }

    public async Task<ServiceResult<EnrollmentResponse>> ApproveEnrollmentAsync(int adminUserId, int enrollmentId, string? message = null)
    {
        _logger.LogInformation("ApproveEnrollmentAsync started — AdminUserId={AdminUserId}, EnrollmentId={EnrollmentId}", adminUserId, enrollmentId);

        var isAdmin = await IsAdminAsync(adminUserId);
        if (!isAdmin)
        {
            _logger.LogWarning("ApproveEnrollmentAsync UNAUTHORIZED — AdminUserId={AdminUserId}", adminUserId);
            return ServiceResult<EnrollmentResponse>.Fail("UNAUTHORIZED", "Bạn không có quyền phê duyệt.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var enrollment = await _context.Enrollments
                .SingleOrDefaultAsync(e => e.EnrollmentId == enrollmentId);

            if (enrollment == null)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning("ApproveEnrollmentAsync ENROLLMENT_NOT_FOUND — EnrollmentId={EnrollmentId}", enrollmentId);
                return ServiceResult<EnrollmentResponse>.Fail("ENROLLMENT_NOT_FOUND", "Không tìm thấy đăng ký học phần.");
            }

            if (!string.Equals(enrollment.Status, EnrollmentStatus.PENDING_APPROVAL.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                await transaction.RollbackAsync();
                _logger.LogWarning("ApproveEnrollmentAsync ENROLLMENT_STATUS_INVALID — EnrollmentId={EnrollmentId}, CurrentStatus={Status}", enrollmentId, enrollment.Status);
                return ServiceResult<EnrollmentResponse>.Fail("ENROLLMENT_STATUS_INVALID", "Trạng thái đăng ký không hợp lệ.");
            }

            enrollment.Status = EnrollmentStatus.ENROLLED.ToString();
            enrollment.UpdatedAt = DateTime.UtcNow;

            var classSectionInfo = await _classSectionRepository.GetClassSectionWithCourseAsync(enrollment.ClassSectionId);
            var (amountPerCredit, defaultSurcharge) = await GetSemesterTuitionRateAsync(enrollment.SemesterId);
            var feeAmount = (classSectionInfo?.Course.Credits ?? enrollment.CreditsSnapshot) * amountPerCredit + defaultSurcharge;

            var walletBalance = await _context.StudentWallets
                .AsNoTracking()
                .Where(w => w.StudentId == enrollment.StudentId)
                .Select(w => (decimal?)w.Balance)
                .SingleOrDefaultAsync() ?? 0m;

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("ApproveEnrollmentAsync SUCCESS — EnrollmentId={EnrollmentId}, AdminUserId={AdminUserId}", enrollmentId, adminUserId);
            return ServiceResult<EnrollmentResponse>.Success(MapEnrollmentResponse(enrollment, feeAmount, walletBalance, message ?? "Đã phê duyệt đăng ký."));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "ApproveEnrollmentAsync EXCEPTION — AdminUserId={AdminUserId}, EnrollmentId={EnrollmentId}", adminUserId, enrollmentId);
            return ServiceResult<EnrollmentResponse>.Fail("SYSTEM_ERROR", "Có lỗi hệ thống, vui lòng thử lại.");
        }
    }

    public async Task<ServiceResult<EnrollmentResponse>> RejectEnrollmentAsync(int adminUserId, int enrollmentId, string? reason = null)
    {
        _logger.LogInformation("RejectEnrollmentAsync started — AdminUserId={AdminUserId}, EnrollmentId={EnrollmentId}", adminUserId, enrollmentId);

        var isAdmin = await IsAdminAsync(adminUserId);
        if (!isAdmin)
        {
            _logger.LogWarning("RejectEnrollmentAsync UNAUTHORIZED — AdminUserId={AdminUserId}", adminUserId);
            return ServiceResult<EnrollmentResponse>.Fail("UNAUTHORIZED", "Bạn không có quyền từ chối đăng ký.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var enrollment = await _context.Enrollments
                .SingleOrDefaultAsync(e => e.EnrollmentId == enrollmentId);

            if (enrollment == null)
            {
                await transaction.RollbackAsync();
                _logger.LogWarning("RejectEnrollmentAsync ENROLLMENT_NOT_FOUND — EnrollmentId={EnrollmentId}", enrollmentId);
                return ServiceResult<EnrollmentResponse>.Fail("ENROLLMENT_NOT_FOUND", "Không tìm thấy đăng ký học phần.");
            }

            if (!string.Equals(enrollment.Status, EnrollmentStatus.PENDING_APPROVAL.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                await transaction.RollbackAsync();
                _logger.LogWarning("RejectEnrollmentAsync ENROLLMENT_STATUS_INVALID — EnrollmentId={EnrollmentId}, CurrentStatus={Status}", enrollmentId, enrollment.Status);
                return ServiceResult<EnrollmentResponse>.Fail("ENROLLMENT_STATUS_INVALID", "Trạng thái đăng ký không hợp lệ.");
            }

            var classSectionInfo = await _classSectionRepository.GetClassSectionWithCourseAsync(enrollment.ClassSectionId);
            if (classSectionInfo == null)
            {
                await transaction.RollbackAsync();
                _logger.LogError("RejectEnrollmentAsync ClassSectionInfo null — ClassSectionId={ClassSectionId}", enrollment.ClassSectionId);
                return ServiceResult<EnrollmentResponse>.Fail("CLASSSECTION_NOT_FOUND", "Không tìm thấy lớp học phần.");
            }

            var classSection = await _classSectionRepository.GetClassSectionForUpdateAsync(enrollment.ClassSectionId);
            if (classSection == null)
            {
                await transaction.RollbackAsync();
                _logger.LogError("RejectEnrollmentAsync ClassSection null after lock — ClassSectionId={ClassSectionId}", enrollment.ClassSectionId);
                return ServiceResult<EnrollmentResponse>.Fail("CLASSSECTION_NOT_FOUND", "Không tìm thấy lớp học phần.");
            }

            var wallet = await _walletRepository.GetWalletForUpdateAsync(enrollment.StudentId);
            if (wallet == null)
            {
                await transaction.RollbackAsync();
                _logger.LogError("RejectEnrollmentAsync Wallet not found — StudentId={StudentId}", enrollment.StudentId);
                return ServiceResult<EnrollmentResponse>.Fail("WALLET_INSUFFICIENT", "Ví sinh viên không tồn tại.");
            }

            var semesterTuitionPolicy = await _semesterTuitionPolicyRepository
                .GetActivePolicyBySemesterAsync(enrollment.SemesterId, DateTime.UtcNow);
            if (semesterTuitionPolicy == null)
            {
                await transaction.RollbackAsync();
                _logger.LogError("RejectEnrollmentAsync TUITION_POLICY_NOT_FOUND — SemesterId={SemesterId}", enrollment.SemesterId);
                return ServiceResult<EnrollmentResponse>.Fail("TUITION_POLICY_NOT_FOUND", "Không tìm thấy chính sách học phí hiệu lực cho học kỳ.");
            }

            var feeAmount = (classSectionInfo.Course.Credits * semesterTuitionPolicy.AmountPerCredit)
                + semesterTuitionPolicy.DefaultSurcharge;

            wallet.Balance += feeAmount;
            wallet.LastUpdated = DateTime.UtcNow;

            classSection.CurrentEnrollment = Math.Max(0, classSection.CurrentEnrollment - 1);

            enrollment.Status = EnrollmentStatus.REJECTED.ToString();
            enrollment.UpdatedAt = DateTime.UtcNow;

            var walletTransaction = new WalletTransaction
            {
                WalletId = wallet.WalletId,
                Amount = feeAmount,
                TransactionType = "REFUND",
                Description = string.IsNullOrWhiteSpace(reason) ? "Hoàn tiền do từ chối đăng ký" : reason.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            await _walletRepository.AddWalletTransactionAsync(walletTransaction);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation("RejectEnrollmentAsync SUCCESS — EnrollmentId={EnrollmentId}, AdminUserId={AdminUserId}", enrollmentId, adminUserId);
            return ServiceResult<EnrollmentResponse>.Success(MapEnrollmentResponse(enrollment, feeAmount, wallet.Balance, "Đã từ chối đăng ký."));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "RejectEnrollmentAsync EXCEPTION — AdminUserId={AdminUserId}, EnrollmentId={EnrollmentId}", adminUserId, enrollmentId);
            return ServiceResult<EnrollmentResponse>.Fail("SYSTEM_ERROR", "Có lỗi hệ thống, vui lòng thử lại.");
        }
    }

    private async Task<bool> HasTimeConflictAsync(int studentId, int semesterId, int classSectionId)
    {
        var targetEvents = await _context.ScheduleEvents
            .AsNoTracking()
            .Where(se => se.ClassSectionId == classSectionId)
            .Select(se => new { se.StartAt, se.EndAt })
            .ToListAsync();

        if (targetEvents.Count == 0)
        {
            return false;
        }

        var studentEvents = await _context.ScheduleEvents
            .AsNoTracking()
            .Where(se => se.ClassSection.Enrollments.Any(e => e.StudentId == studentId
                && e.SemesterId == semesterId
                && ActiveScheduleStatuses.Contains(e.Status)))
            .Select(se => new { se.StartAt, se.EndAt })
            .ToListAsync();

        if (studentEvents.Count == 0)
        {
            return false;
        }

        foreach (var targetEvent in targetEvents)
        {
            if (studentEvents.Any(se => targetEvent.StartAt < se.EndAt && targetEvent.EndAt > se.StartAt))
            {
                return true;
            }
        }

        return false;
    }

    private static EnrollmentResponse MapEnrollmentResponse(Enrollment enrollment, decimal feeAmount, decimal walletBalance, string? message)
    {
        return new EnrollmentResponse
        {
            EnrollmentId = enrollment.EnrollmentId,
            Status = enrollment.Status,
            ClassSectionId = enrollment.ClassSectionId,
            CourseId = enrollment.CourseId,
            SemesterId = enrollment.SemesterId,
            Credits = enrollment.CreditsSnapshot,
            FeeAmount = feeAmount,
            WalletBalance = walletBalance,
            Message = message
        };
    }

    private async Task<bool> IsAdminAsync(int userId)
    {
        var user = await _userRepository.GetUserByIdAsync(userId);
        return user != null && string.Equals(user.Role, UserRole.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(decimal AmountPerCredit, decimal DefaultSurcharge)> GetSemesterTuitionRateAsync(int semesterId, CancellationToken ct = default)
    {
        var policy = await _semesterTuitionPolicyRepository.GetActivePolicyBySemesterAsync(semesterId, DateTime.UtcNow, ct);
        if (policy == null)
        {
            _logger.LogWarning("GetSemesterTuitionRateAsync POLICY_NOT_FOUND — SemesterId={SemesterId}", semesterId);
            return (FallbackAmountPerCredit, FallbackSurcharge);
        }

        return (policy.AmountPerCredit, policy.DefaultSurcharge);
    }

    public async Task<IReadOnlyList<ClassSectionSummaryViewModel>> GetOpenClassSectionsAsync()
    {
        var classSectionSemesterLookup = await _context.ClassSections
            .AsNoTracking()
            .Select(cs => new { cs.ClassSectionId, cs.SemesterId })
            .ToDictionaryAsync(x => x.ClassSectionId, x => x.SemesterId);

        var items = await _context.vw_ClassSectionSummaries
            .AsNoTracking()
            .Where(cs => cs.IsOpen)
            .OrderBy(cs => cs.CourseCode)
            .ThenBy(cs => cs.SectionCode)
            .ToListAsync();

        var semesterIds = classSectionSemesterLookup.Values.Distinct().ToList();
        var now = DateTime.UtcNow;
        var policyBySemesterId = await _context.SemesterTuitionPolicies
            .AsNoTracking()
            .Where(p => semesterIds.Contains(p.SemesterId)
                && p.IsActive
                && p.EffectiveFrom <= now
                && (!p.EffectiveTo.HasValue || p.EffectiveTo.Value >= now))
            .GroupBy(p => p.SemesterId)
            .Select(g => g.OrderByDescending(p => p.EffectiveFrom).First())
            .ToDictionaryAsync(p => p.SemesterId);

        return items.Select(cs =>
        {
            classSectionSemesterLookup.TryGetValue(cs.ClassSectionId, out var semesterId);
            policyBySemesterId.TryGetValue(semesterId, out var policy);
            var amountPerCredit = policy?.AmountPerCredit ?? FallbackAmountPerCredit;
            var surcharge = policy?.DefaultSurcharge ?? FallbackSurcharge;

            return new ClassSectionSummaryViewModel
            {
                ClassSectionId = cs.ClassSectionId,
                CourseCode = cs.CourseCode,
                CourseName = cs.CourseName,
                Credits = cs.Credits,
                SectionCode = cs.SectionCode,
                SemesterCode = cs.SemesterCode,
                SemesterId = semesterId,
                TeacherFullName = cs.TeacherFullName,
                CurrentEnrollment = cs.CurrentEnrollment,
                MaxCapacity = cs.MaxCapacity,
                IsOpen = cs.IsOpen,
                EstimatedFee = cs.Credits * amountPerCredit + surcharge
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<PendingEnrollmentViewModel>> GetPendingEnrollmentsAsync()
    {
        var pendingStatus = EnrollmentStatus.PENDING_APPROVAL.ToString();

        var items = await _context.Enrollments
            .AsNoTracking()
            .Include(e => e.Student).ThenInclude(s => s.StudentNavigation)
            .Include(e => e.ClassSection)
            .Include(e => e.Course)
            .Where(e => e.Status == pendingStatus)
            .OrderByDescending(e => e.EnrolledAt)
            .ToListAsync();

        var semesterIds = items.Select(i => i.SemesterId).Distinct().ToList();
        var now = DateTime.UtcNow;
        var policyBySemesterId = await _context.SemesterTuitionPolicies
            .AsNoTracking()
            .Where(p => semesterIds.Contains(p.SemesterId)
                && p.IsActive
                && p.EffectiveFrom <= now
                && (!p.EffectiveTo.HasValue || p.EffectiveTo.Value >= now))
            .GroupBy(p => p.SemesterId)
            .Select(g => g.OrderByDescending(p => p.EffectiveFrom).First())
            .ToDictionaryAsync(p => p.SemesterId);

        return items.Select(e =>
        {
            policyBySemesterId.TryGetValue(e.SemesterId, out var policy);
            var amountPerCredit = policy?.AmountPerCredit ?? FallbackAmountPerCredit;
            var surcharge = policy?.DefaultSurcharge ?? FallbackSurcharge;

            return new PendingEnrollmentViewModel
            {
                EnrollmentId = e.EnrollmentId,
                StudentId = e.StudentId,
                StudentCode = e.Student.StudentCode,
                StudentFullName = e.Student.StudentNavigation.FullName,
                ClassSectionId = e.ClassSectionId,
                CourseCode = e.Course.CourseCode,
                SectionCode = e.ClassSection.SectionCode,
                Credits = e.CreditsSnapshot,
                FeeAmount = e.CreditsSnapshot * amountPerCredit + surcharge,
                EnrolledAt = e.EnrolledAt
            };
        }).ToList();
    }

    public async Task<WalletBalanceResponse?> GetWalletBalanceAsync(int userId)
    {
        var wallet = await _context.StudentWallets
            .AsNoTracking()
            .Include(w => w.Student)
            .SingleOrDefaultAsync(w => w.Student.StudentId == userId);

        if (wallet == null)
        {
            return null;
        }

        return new WalletBalanceResponse
        {
            WalletId = wallet.WalletId,
            Balance = wallet.Balance,
            LastUpdated = wallet.LastUpdated
        };
    }

    public async Task<RegistrationSummaryDto> GetRegistrationSummaryAsync(int userId, int? semesterId = null)
    {
        var allSemesters = await _semesterRepository.GetAllSemestersAsync();

        var semesterOptions = allSemesters.Select(s => new SemesterOptionDto
        {
            SemesterId = s.SemesterId,
            SemesterCode = s.SemesterCode,
            SemesterName = s.SemesterName,
            IsActive = s.IsActive,
            StartDate = s.StartDate,
            EndDate = s.EndDate
        }).ToList();

        var currentSemester = allSemesters.FirstOrDefault(s => s.IsActive)
            ?? allSemesters.FirstOrDefault();

        BusinessObject.Entities.Semester? selectedSemester = null;

        if (semesterId.HasValue)
        {
            selectedSemester = allSemesters.FirstOrDefault(s => s.SemesterId == semesterId.Value);
        }

        selectedSemester ??= currentSemester;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var isPastSemester = currentSemester != null
            && selectedSemester != null
            && selectedSemester.StartDate < currentSemester.StartDate;

        var isRegistrationClosed = selectedSemester != null
            && ((selectedSemester.RegistrationEndDate.HasValue && today > selectedSemester.RegistrationEndDate.Value)
                || (selectedSemester.AddDropDeadline.HasValue && today > selectedSemester.AddDropDeadline.Value));

        var canRegisterInSemester = !isPastSemester && !isRegistrationClosed;

        IReadOnlyList<ClassSectionSummaryViewModel> classSections = [];
        decimal? walletBalance = null;

        if (selectedSemester != null)
        {
            var (amountPerCredit, defaultSurcharge) = await GetSemesterTuitionRateAsync(selectedSemester.SemesterId);

            var semesterIdLookup = await _context.ClassSections
                .AsNoTracking()
                .Where(cs => cs.Semester.SemesterCode == selectedSemester.SemesterCode)
                .Select(cs => new { cs.ClassSectionId, cs.SemesterId })
                .ToDictionaryAsync(x => x.ClassSectionId, x => x.SemesterId);

            var items = await _context.vw_ClassSectionSummaries
                .AsNoTracking()
                .Where(cs => cs.SemesterCode == selectedSemester.SemesterCode && cs.IsOpen)
                .OrderBy(cs => cs.CourseCode)
                .ThenBy(cs => cs.SectionCode)
                .ToListAsync();

            classSections = items.Select(cs =>
            {
                var csSemId = semesterIdLookup.GetValueOrDefault(cs.ClassSectionId, selectedSemester.SemesterId);
                var isFull = cs.CurrentEnrollment >= cs.MaxCapacity;

                return new ClassSectionSummaryViewModel
                {
                    ClassSectionId = cs.ClassSectionId,
                    CourseCode = cs.CourseCode,
                    CourseName = cs.CourseName,
                    Credits = cs.Credits,
                    SectionCode = cs.SectionCode,
                    SemesterCode = cs.SemesterCode,
                    SemesterId = csSemId,
                    TeacherFullName = cs.TeacherFullName,
                    CurrentEnrollment = cs.CurrentEnrollment,
                    MaxCapacity = cs.MaxCapacity,
                    IsOpen = cs.IsOpen,
                    EstimatedFee = cs.Credits * amountPerCredit + defaultSurcharge,
                    CanRegister = canRegisterInSemester && cs.IsOpen && !isFull
                };
            }).ToList();
        }

        var wallet = await _context.StudentWallets
            .AsNoTracking()
            .SingleOrDefaultAsync(w => w.StudentId == userId);

        if (wallet != null)
        {
            walletBalance = wallet.Balance;
        }

        return new RegistrationSummaryDto
        {
            SelectedSemesterId = selectedSemester?.SemesterId,
            SelectedSemesterCode = selectedSemester?.SemesterCode,
            CurrentSemesterId = currentSemester?.SemesterId,
            IsPastSemester = isPastSemester,
            IsRegistrationClosed = isRegistrationClosed,
            RegistrationEndDate = selectedSemester?.RegistrationEndDate,
            AddDropDeadline = selectedSemester?.AddDropDeadline,
            Semesters = semesterOptions,
            ClassSections = classSections,
            WalletBalance = walletBalance
        };
    }

    public async Task<MyCoursesPageDto> GetMyCoursesAsync(int userId, int? semesterId, int page = 1, int pageSize = 10)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 10;

        var student = await _studentRepository.GetStudentByUserIdAsync(userId);
        if (student == null)
        {
            _logger.LogWarning("GetMyCoursesAsync STUDENT_NOT_FOUND — UserId={UserId}", userId);
            return new MyCoursesPageDto { Page = page, PageSize = pageSize };
        }

        var allSemesters = await _semesterRepository.GetAllSemestersAsync();
        var semesterOptions = allSemesters.Select(s => new SemesterOptionDto
        {
            SemesterId = s.SemesterId,
            SemesterCode = s.SemesterCode,
            SemesterName = s.SemesterName,
            IsActive = s.IsActive,
            StartDate = s.StartDate,
            EndDate = s.EndDate
        }).ToList();

        var (items, totalCount) = await _enrollmentRepository.GetStudentEnrollmentsAsync(
            student.StudentId, semesterId, page, pageSize);

        var dtoItems = items.Select(e => new MyCourseItemDto
        {
            EnrollmentId = e.EnrollmentId,
            ClassSectionId = e.ClassSectionId,
            CourseCode = e.Course.CourseCode,
            CourseName = e.Course.CourseName,
            Credits = e.CreditsSnapshot,
            SemesterCode = e.Semester.SemesterCode,
            SectionCode = e.ClassSection.SectionCode,
            TeacherName = e.ClassSection.Teacher.TeacherNavigation.FullName,
            Status = e.Status,
            EnrolledAt = e.EnrolledAt
        }).ToList();

        return new MyCoursesPageDto
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            SemesterId = semesterId,
            Items = dtoItems,
            Semesters = semesterOptions
        };
    }
}
