using BusinessLogic.DTOs.Requests.GradeAppeals;
using BusinessLogic.DTOs.Responses.GradeAppeals;
using BusinessLogic.DTOs.Responses;

namespace BusinessLogic.Services.Interfaces;

public interface IGradeAppealService
{
    Task<ServiceResult<GradeAppealDetailDto>> SubmitAppealAsync(int userId, SubmitGradeAppealRequest request, CancellationToken ct = default);
    Task<ServiceResult<GradeAppealDetailDto>> StartReviewAsync(int reviewerUserId, long appealId, CancellationToken ct = default);
    Task<ServiceResult<GradeAppealDetailDto>> ResolveAppealAsync(int reviewerUserId, ResolveGradeAppealRequest request, CancellationToken ct = default);
    Task<ServiceResult<GradeAppealDetailDto>> GetDetailAsync(int userId, long appealId, CancellationToken ct = default);
    Task<ServiceResult<PagedResult<GradeAppealListItemDto>>> GetPagedAsync(int userId, GradeAppealQueryRequest query, CancellationToken ct = default);
    Task<IReadOnlyList<SemesterOptionDto>> GetSemesterOptionsAsync(CancellationToken ct = default);
}
