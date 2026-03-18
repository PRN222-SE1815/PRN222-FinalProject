using BusinessLogic.DTOs.Requests.Gradebook;
using BusinessLogic.DTOs.Response;
using BusinessLogic.DTOs.Responses.Gradebook;
using BusinessObject.Entities;

namespace BusinessLogic.Services.Interfaces;

public interface IGradebookService
{
    Task<ServiceResult<IReadOnlyList<TeacherClassSectionDto>>> GetTeacherClassSectionsAsync(int teacherUserId, string actorRole, CancellationToken ct = default);

    Task<ServiceResult<GradebookDetailResponse>> GetGradebookAsync(int actorUserId, string actorRole, int classSectionId, CancellationToken ct = default);

    Task<ServiceResult<bool>> UpsertTeacherScoresAsync(int teacherUserId, string actorRole, UpsertScoresRequest request, CancellationToken ct = default);

    Task<ServiceResult<GradebookApprovalResponse>> RequestApprovalAsync(int teacherUserId, string actorRole, RequestApprovalRequest request, CancellationToken ct = default);

    Task<ServiceResult<GradebookApprovalResponse>> ApproveGradebookAsync(int adminUserId, string actorRole, ApproveGradebookRequest request, CancellationToken ct = default);

    Task<ServiceResult<GradebookApprovalResponse>> RejectGradebookAsync(int adminUserId, string actorRole, RejectGradebookRequest request, CancellationToken ct = default);

    Task<ServiceResult<(IReadOnlyList<GradeBook> Items, int TotalCount)>> GetPagedGradebooksForAdminAsync(int adminUserId, string actorRole, string? statusFilter, int page, int pageSize, CancellationToken ct = default);

    Task<ServiceResult<int?>> GetActiveSemesterIdAsync(CancellationToken ct = default);
}
