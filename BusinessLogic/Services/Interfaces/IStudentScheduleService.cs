using BusinessLogic.DTOs.Request;
using BusinessLogic.DTOs.Response;

namespace BusinessLogic.Services.Interfaces;

public interface IStudentScheduleService
{
    Task<ServiceResult<StudentCalendarResponseDto>> GetStudentCalendarAsync(
        int userId,
        GetStudentCalendarRequest request,
        CancellationToken cancellationToken = default);
}
