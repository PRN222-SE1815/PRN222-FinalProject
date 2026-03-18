using BusinessLogic.DTOs.Request;
using BusinessLogic.DTOs.Response;

namespace BusinessLogic.Services.Interfaces;

public interface ITeacherScheduleService
{
    Task<ServiceResult<StudentCalendarResponseDto>> GetTeacherCalendarAsync(
        int userId,
        GetStudentCalendarRequest request,
        CancellationToken cancellationToken = default);
}
