using System.Security.Claims;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Student.Pages.Wallet;

[Authorize(Roles = nameof(UserRole.STUDENT))]
public class DepositModel : PageModel
{
    private readonly IPaymentService _paymentService;
    private readonly IEnrollmentService _enrollmentService;
    private readonly ILogger<DepositModel> _logger;

    public DepositModel(IPaymentService paymentService, IEnrollmentService enrollmentService, ILogger<DepositModel> logger)
    {
        _paymentService = paymentService;
        _enrollmentService = enrollmentService;
        _logger = logger;
    }

    [BindProperty]
    public decimal Amount { get; set; }

    public decimal? WalletBalance { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            var userId = GetUserId();
            var wallet = await _enrollmentService.GetWalletBalanceAsync(userId);
            WalletBalance = wallet?.Balance;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deposit OnGetAsync error");
            ErrorMessage = "Có lỗi xảy ra, vui lòng thử lại.";
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        try
        {
            var userId = GetUserId();
            var result = await _paymentService.CreateDepositAsync(userId, Amount);

            if (result.IsSuccess && result.Data != null)
            {
                _logger.LogInformation("Deposit redirect to MoMo — OrderId={OrderId}", result.Data.OrderId);
                return Redirect(result.Data.PayUrl);
            }

            ErrorMessage = result.Message ?? "Không thể tạo giao dịch.";
            var wallet = await _enrollmentService.GetWalletBalanceAsync(userId);
            WalletBalance = wallet?.Balance;
            return Page();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deposit OnPostAsync error");
            ErrorMessage = "Có lỗi xảy ra, vui lòng thử lại.";
            return Page();
        }
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null ? int.Parse(claim.Value) : 0;
    }
}
