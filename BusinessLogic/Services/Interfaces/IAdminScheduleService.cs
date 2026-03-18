using BusinessLogic.DTOs.Request;
using BusinessLogic.DTOs.Response;

namespace BusinessLogic.Services.Interfaces;

public interface IAdminScheduleService
{
    Task<ServiceResult<AdminSchedulePageDto>> GetSchedulesAsync(
        int adminUserId,
        AdminScheduleFilterRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminScheduleItemDto>> GetScheduleDetailAsync(
        int adminUserId,
        long scheduleEventId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminScheduleItemDto>> CreateScheduleEventAsync(
        int adminUserId,
        CreateScheduleEventRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AdminScheduleItemDto>> UpdateScheduleEventAsync(
        int adminUserId,
        UpdateScheduleEventRequest request,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> ChangeScheduleStatusAsync(
        int adminUserId,
        long scheduleEventId,
        string newStatus,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
