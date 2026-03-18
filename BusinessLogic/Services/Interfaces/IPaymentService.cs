using BusinessLogic.DTOs.Request;
using BusinessLogic.DTOs.Response;

namespace BusinessLogic.Services.Interfaces;

public interface IPaymentService
{
    Task<ServiceResult<MoMoCreatePaymentResponse>> CreateDepositAsync(int userId, decimal amount);
    Task<ServiceResult> HandleMoMoCallbackAsync(MoMoCallbackRequest payload);
}
