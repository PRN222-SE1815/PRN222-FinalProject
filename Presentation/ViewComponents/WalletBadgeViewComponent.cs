using System.Security.Claims;
using BusinessLogic.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Presentation.ViewComponents;

public class WalletBadgeViewComponent : ViewComponent
{
    private readonly IEnrollmentService _enrollmentService;

    public WalletBadgeViewComponent(IEnrollmentService enrollmentService)
    {
        _enrollmentService = enrollmentService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userPrincipal = HttpContext.User;
        var roleClaim = userPrincipal.FindFirst(ClaimTypes.Role)?.Value;

        if (!string.Equals(roleClaim, "STUDENT", StringComparison.OrdinalIgnoreCase))
        {
            return Content(string.Empty);
        }

        var userIdClaim = userPrincipal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
        {
            return Content(string.Empty);
        }

        decimal balance = 0;
        var wallet = await _enrollmentService.GetWalletBalanceAsync(userId);
        if (wallet != null)
        {
            balance = wallet.Balance;
        }

        return View(balance);
    }
}
