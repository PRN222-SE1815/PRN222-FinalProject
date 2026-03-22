using BusinessLogic.DTOs.Requests;
using BusinessLogic.DTOs.Responses;

namespace BusinessLogic.Services.Interfaces;

public interface IPaymentService
{
    Task<ServiceResult<MoMoCreatePaymentResponse>> CreateDepositAsync(int userId, decimal amount, string? returnUrl = null, string? notifyUrl = null);
    Task<ServiceResult> HandleMoMoCallbackAsync(MoMoCallbackRequest payload);
}
