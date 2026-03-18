using System.Security.Claims;
using System.Text.Json;
using BusinessLogic.DTOs.Response;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Student.Pages.CourseRegistration;

[Authorize(Roles = nameof(UserRole.STUDENT))]
public class IndexModel : PageModel
{
    private readonly IEnrollmentService _enrollmentService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IEnrollmentService enrollmentService, ILogger<IndexModel> logger)
    {
        _enrollmentService = enrollmentService;
        _logger = logger;
    }

    public RegistrationSummaryDto Summary { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? SemesterId { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? MissingPrerequisitesJson { get; set; }

    public IReadOnlyList<PrerequisiteInfoDto> MissingPrerequisites { get; set; } = [];

    public async Task OnGetAsync()
    {
        try
        {
            var userId = GetUserId();
            Summary = await _enrollmentService.GetRegistrationSummaryAsync(userId, SemesterId);

            if (!string.IsNullOrEmpty(MissingPrerequisitesJson))
            {
                MissingPrerequisites = JsonSerializer.Deserialize<List<PrerequisiteInfoDto>>(MissingPrerequisitesJson) ?? [];
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CourseRegistration OnGetAsync error — SemesterId={SemesterId}", SemesterId);
            ErrorMessage = "Có lỗi xảy ra, vui lòng thử lại.";
        }
    }

    public async Task<IActionResult> OnPostRegisterAsync(int classSectionId, int? semesterId)
    {
        try
        {
            var userId = GetUserId();
            var result = await _enrollmentService.RegisterAndPayAsync(userId, classSectionId);

            if (result.IsSuccess)
            {
                SuccessMessage = result.Data?.Message ?? "Đăng ký thành công, chờ phê duyệt.";
            }
            else
            {
                ErrorMessage = result.Message;

                if (string.Equals(result.ErrorCode, "PREREQ_NOT_MET", StringComparison.OrdinalIgnoreCase)
                    && result.Data?.MissingPrerequisites is { Count: > 0 } missing)
                {
                    MissingPrerequisitesJson = JsonSerializer.Serialize(missing);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CourseRegistration OnPostRegisterAsync error — ClassSectionId={ClassSectionId}", classSectionId);
            ErrorMessage = "Có lỗi xảy ra, vui lòng thử lại.";
        }

        return RedirectToPage(new { semesterId });
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null ? int.Parse(claim.Value) : 0;
    }
}
