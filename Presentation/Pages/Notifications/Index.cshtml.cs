using System.Security.Claims;
using BusinessLogic.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Pages.Notifications;

[Authorize]
public sealed class IndexModel : PageModel
{
    private readonly INotificationService _notificationService;

    public IndexModel(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public IActionResult OnGet()
    {
        return NotFound();
    }

    public async Task<IActionResult> OnGetNotificationsAsync(int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return Unauthorized();
        }

        var result = await _notificationService.GetMyNotificationsAsync(userId, page, pageSize, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { result.ErrorCode, result.Message });
        }

        return new JsonResult(result.Data);
    }

    public async Task<IActionResult> OnGetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return Unauthorized();
        }

        var result = await _notificationService.GetMyUnreadCountAsync(userId, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { result.ErrorCode, result.Message });
        }

        return new JsonResult(new { Count = result.Data });
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostMarkReadAsync(long notificationId, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return Unauthorized();
        }

        var result = await _notificationService.MarkAsReadAsync(userId, notificationId, cancellationToken);
        if (!result.IsSuccess)
        {
            var statusCode = result.ErrorCode == "NOT_FOUND" ? 404 : 400;
            return StatusCode(statusCode, new { result.ErrorCode, result.Message });
        }

        return new JsonResult(new { Success = true });
    }

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OnPostMarkAllReadAsync(CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId <= 0)
        {
            return Unauthorized();
        }

        var result = await _notificationService.MarkAllAsReadAsync(userId, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { result.ErrorCode, result.Message });
        }

        return new JsonResult(new { Success = true });
    }

    private int GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var userId) ? userId : 0;
    }
}
