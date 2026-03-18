using System.Security.Claims;
using BusinessLogic.DTOs.GradeAppeals;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Student.Pages.GradeAppeals;

[Authorize(Roles = nameof(UserRole.STUDENT))]
public class DetailModel : PageModel
{
    private readonly IGradeAppealService _appealService;
    private readonly ILogger<DetailModel> _logger;

    public DetailModel(IGradeAppealService appealService, ILogger<DetailModel> logger)
    {
        _appealService = appealService;
        _logger = logger;
    }

    public GradeAppealDetailDto? Appeal { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(long id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        try
        {
            var result = await _appealService.GetDetailAsync(userId, id, ct);
            if (result.IsSuccess && result.Data is not null)
            {
                Appeal = result.Data;
            }
            else
            {
                ErrorMessage = result.Message;
                return RedirectToPage("/GradeAppeals/Index", new { area = "Student" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading appeal detail {AppealId}", id);
            ErrorMessage = "Đã xảy ra lỗi khi tải chi tiết khiếu nại.";
            return RedirectToPage("/GradeAppeals/Index", new { area = "Student" });
        }

        return Page();
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var id) ? id : 0;
    }
}
