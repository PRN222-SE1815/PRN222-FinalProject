using BusinessLogic.DTOs.Request;
using BusinessLogic.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Pages.Payment;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class MoMoNotifyModel : PageModel
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<MoMoNotifyModel> _logger;

    public MoMoNotifyModel(IPaymentService paymentService, ILogger<MoMoNotifyModel> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    public IActionResult OnGet() => NotFound();

    public async Task<IActionResult> OnPostAsync([FromBody] MoMoCallbackRequest payload)
    {
        if (payload == null || string.IsNullOrEmpty(payload.OrderId))
        {
            _logger.LogWarning("MoMoNotify received invalid payload");
            return BadRequest();
        }

        _logger.LogInformation("MoMoNotify received — OrderId={OrderId}, ResultCode={ResultCode}", payload.OrderId, payload.ResultCode);

        try
        {
            var result = await _paymentService.HandleMoMoCallbackAsync(payload);

            if (!result.IsSuccess && string.Equals(result.ErrorCode, "INVALID_SIGNATURE", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("MoMoNotify INVALID_SIGNATURE — OrderId={OrderId}", payload.OrderId);
                return BadRequest();
            }

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MoMoNotify EXCEPTION — OrderId={OrderId}", payload.OrderId);
            return StatusCode(500);
        }
    }
}
