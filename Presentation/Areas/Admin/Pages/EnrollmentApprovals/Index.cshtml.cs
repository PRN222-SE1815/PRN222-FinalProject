using System.Security.Claims;
using BusinessLogic.DTOs.Response;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Admin.Pages.EnrollmentApprovals;

[Authorize(Roles = nameof(UserRole.ADMIN))]
public class IndexModel : PageModel
{
    private readonly IEnrollmentService _enrollmentService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IEnrollmentService enrollmentService, ILogger<IndexModel> logger)
    {
        _enrollmentService = enrollmentService;
        _logger = logger;
    }

    public IReadOnlyList<PendingEnrollmentViewModel> PendingEnrollments { get; set; } = [];

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        try
        {
            PendingEnrollments = await _enrollmentService.GetPendingEnrollmentsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnrollmentApprovals OnGetAsync error");
            ErrorMessage = "Có lỗi xảy ra, vui lòng thử lại.";
        }
    }

    public async Task<IActionResult> OnPostApproveAsync(int enrollmentId)
    {
        try
        {
            var adminUserId = GetUserId();
            var result = await _enrollmentService.ApproveEnrollmentAsync(adminUserId, enrollmentId);

            if (result.IsSuccess)
            {
                SuccessMessage = result.Data?.Message ?? "Đã phê duyệt đăng ký.";
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnrollmentApprovals OnPostApproveAsync error — EnrollmentId={EnrollmentId}", enrollmentId);
            ErrorMessage = "Có lỗi xảy ra, vui lòng thử lại.";
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRejectAsync(int enrollmentId, string? reason)
    {
        try
        {
            var adminUserId = GetUserId();
            var result = await _enrollmentService.RejectEnrollmentAsync(adminUserId, enrollmentId, reason);

            if (result.IsSuccess)
            {
                SuccessMessage = result.Data?.Message ?? "Đã từ chối đăng ký.";
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "EnrollmentApprovals OnPostRejectAsync error — EnrollmentId={EnrollmentId}", enrollmentId);
            ErrorMessage = "Có lỗi xảy ra, vui lòng thử lại.";
        }

        return RedirectToPage();
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null ? int.Parse(claim.Value) : 0;
    }
}
