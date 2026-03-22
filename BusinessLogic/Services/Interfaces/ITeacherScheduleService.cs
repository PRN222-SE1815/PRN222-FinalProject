using BusinessLogic.DTOs.Requests;
using BusinessLogic.DTOs.Responses;

namespace BusinessLogic.Services.Interfaces;

public interface ITeacherScheduleService
{
    Task<ServiceResult<StudentCalendarResponseDto>> GetTeacherCalendarAsync(
        int userId,
        GetStudentCalendarRequest request,
        CancellationToken cancellationToken = default);
}
