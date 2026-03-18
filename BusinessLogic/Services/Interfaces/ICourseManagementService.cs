using BusinessLogic.DTOs.Requests.CourseManagement;
using BusinessLogic.DTOs.Response;
using BusinessLogic.DTOs.Responses.CourseManagement;

namespace BusinessLogic.Services.Interfaces;

public interface ICourseManagementService
{
    Task<ServiceResult<PagedResultDto>> GetCoursesAsync(int actorUserId, string actorRole, GetCoursesRequest request, CancellationToken ct = default);
    Task<ServiceResult<CourseDetailResponse>> GetCourseDetailAsync(int actorUserId, string actorRole, int courseId, CancellationToken ct = default);
    Task<ServiceResult<CourseDetailResponse>> CreateCourseAsync(int actorUserId, string actorRole, CreateCourseRequest request, CancellationToken ct = default);
    Task<ServiceResult<CourseDetailResponse>> UpdateCourseAsync(int actorUserId, string actorRole, UpdateCourseRequest request, CancellationToken ct = default);
    Task<ServiceResult<DeactivateCourseResultResponse>> DeactivateCourseAsync(int actorUserId, string actorRole, DeactivateCourseRequest request, CancellationToken ct = default);

    Task<ServiceResult<IReadOnlyList<SemesterOptionDto>>> GetAllSemesterOptionsAsync(int actorUserId, string actorRole, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<CourseSectionDto>>> GetCourseSectionsAsync(int actorUserId, string actorRole, int courseId, CancellationToken ct = default);
}
