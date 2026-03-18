using BusinessLogic.DTOs.Response;

namespace BusinessLogic.Services.Interfaces;

public interface IEnrollmentService
{
    Task<ServiceResult<EnrollmentResponse>> RegisterAndPayAsync(int userId, int classSectionId);
    Task<ServiceResult<EnrollmentResponse>> ApproveEnrollmentAsync(int adminUserId, int enrollmentId, string? message = null);
    Task<ServiceResult<EnrollmentResponse>> RejectEnrollmentAsync(int adminUserId, int enrollmentId, string? reason = null);
    Task<IReadOnlyList<ClassSectionSummaryViewModel>> GetOpenClassSectionsAsync();
    Task<IReadOnlyList<PendingEnrollmentViewModel>> GetPendingEnrollmentsAsync();
    Task<WalletBalanceResponse?> GetWalletBalanceAsync(int userId);
    Task<RegistrationSummaryDto> GetRegistrationSummaryAsync(int userId, int? semesterId = null);
    Task<MyCoursesPageDto> GetMyCoursesAsync(int userId, int? semesterId, int page = 1, int pageSize = 10);
}
