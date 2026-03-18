using System.Security.Claims;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Entities;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Admin.Pages.GradebookManagement;

[Authorize(Roles = nameof(UserRole.ADMIN))]
public class IndexModel : PageModel
{
    private const int DefaultPageSize = 10;

    private readonly IGradebookService _gradebookService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IGradebookService gradebookService, ILogger<IndexModel> logger)
    {
        _gradebookService = gradebookService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? StatusFilter { get; set; }

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    public IReadOnlyList<GradeBook> Gradebooks { get; set; } = [];
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        if (string.IsNullOrEmpty(StatusFilter))
        {
            StatusFilter = "PENDING_APPROVAL";
        }

        try
        {
            if (CurrentPage <= 0) CurrentPage = 1;

            var result = await _gradebookService.GetPagedGradebooksForAdminAsync(
                userId,
                nameof(UserRole.ADMIN),
                StatusFilter == "ALL" ? null : StatusFilter,
                CurrentPage,
                DefaultPageSize,
                ct);

            if (result.IsSuccess)
            {
                Gradebooks = result.Data.Items;
                TotalCount = result.Data.TotalCount;
                TotalPages = TotalCount > 0 ? (int)Math.Ceiling((double)TotalCount / DefaultPageSize) : 1;
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading gradebooks for admin");
            ErrorMessage = "An unexpected error occurred while loading gradebooks.";
        }

        return Page();
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var id) ? id : 0;
    }
}
