using System.Security.Claims;
using BusinessLogic.DTOs.Requests.AdminAnalytics;
using BusinessLogic.DTOs.Responses.AdminAnalytics;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Admin.Pages.Reports;

[Authorize(Roles = nameof(UserRole.ADMIN))]
public class IndexModel : PageModel
{
    private readonly IAdminAnalyticsService _adminAnalyticsService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IAdminAnalyticsService adminAnalyticsService, ILogger<IndexModel> logger)
    {
        _adminAnalyticsService = adminAnalyticsService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int? SemesterId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? CompareSemesterId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool IncludeAllSemesters { get; set; }

    public AdminAnalyticsDashboardDto? Dashboard { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var adminUserId = GetUserId();
        if (adminUserId == 0)
        {
            return RedirectToPage("/Account/Login");
        }

        try
        {
            var result = await _adminAnalyticsService.GetDashboardAsync(
                adminUserId,
                new AdminAnalyticsQueryRequest
                {
                    SemesterId = SemesterId,
                    CompareSemesterId = CompareSemesterId,
                    IncludeAllSemesters = IncludeAllSemesters
                },
                ct);

            if (!result.IsSuccess || result.Data is null)
            {
                ErrorMessage = result.Message;
                return Page();
            }

            Dashboard = result.Data;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin reports dashboard load failed. UserId={UserId}", adminUserId);
            ErrorMessage = "Unable to load analytics dashboard.";
        }

        return Page();
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var id) ? id : 0;
    }
}
