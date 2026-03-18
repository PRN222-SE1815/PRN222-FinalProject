using BusinessLogic.DTOs.Request;
using BusinessLogic.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Pages.Payment;

public class MoMoReturnModel : PageModel
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<MoMoReturnModel> _logger;

    public MoMoReturnModel(IPaymentService paymentService, ILogger<MoMoReturnModel> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    public int ResultCode { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsSuccess => ResultCode == 0;

    public async Task OnGetAsync(
        [FromQuery] string? partnerCode,
        [FromQuery] string? orderId,
        [FromQuery] string? requestId,
        [FromQuery] long amount,
        [FromQuery] string? orderInfo,
        [FromQuery] string? orderType,
        [FromQuery] long transId,
        [FromQuery] int resultCode,
        [FromQuery] string? message,
        [FromQuery] string? payType,
        [FromQuery] long responseTime,
        [FromQuery] string? extraData,
        [FromQuery] string? signature)
    {
        ResultCode = resultCode;
        OrderId = orderId ?? string.Empty;
        Message = message ?? string.Empty;

        // IPN (NotifyUrl) cannot reach localhost in dev, so we process here as fallback.
        // HandleMoMoCallbackAsync is idempotent — safe to call from both ReturnUrl and NotifyUrl.
        var payload = new MoMoCallbackRequest
        {
            PartnerCode = partnerCode ?? string.Empty,
            OrderId = orderId ?? string.Empty,
            RequestId = requestId ?? string.Empty,
            Amount = amount,
            OrderInfo = orderInfo ?? string.Empty,
            OrderType = orderType ?? string.Empty,
            TransId = transId,
            ResultCode = resultCode,
            Message = message ?? string.Empty,
            PayType = payType ?? string.Empty,
            ResponseTime = responseTime,
            ExtraData = extraData ?? string.Empty,
            Signature = signature ?? string.Empty
        };

        try
        {
            var result = await _paymentService.HandleMoMoCallbackAsync(payload);

            if (!result.IsSuccess && !string.Equals(result.ErrorCode, "PAYMENT_FAILED", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("MoMoReturn callback processing failed — OrderId={OrderId}, ErrorCode={ErrorCode}, Message={Message}",
                    OrderId, result.ErrorCode, result.Message);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MoMoReturn callback processing exception — OrderId={OrderId}", OrderId);
        }
    }
}
