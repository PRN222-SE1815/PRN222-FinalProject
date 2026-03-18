using System.Security.Claims;
using BusinessLogic.DTOs.Request;
using BusinessLogic.DTOs.Response;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Admin.Pages.ScheduleManagement;

[Authorize(Roles = nameof(UserRole.ADMIN))]
public class IndexModel : PageModel
{
    private const int MinPageSize = 10;
    private const int MaxPageSize = 100;

    private readonly IAdminScheduleService _adminScheduleService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IAdminScheduleService adminScheduleService, ILogger<IndexModel> logger)
    {
        _adminScheduleService = adminScheduleService;
        _logger = logger;
    }

    public AdminSchedulePageDto SchedulePage { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int Page { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 20;

    [BindProperty(SupportsGet = true)]
    public DateTime? From { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? To { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? SemesterId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? ClassSectionId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? TeacherId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public int TotalPages => SchedulePage.TotalCount > 0
        ? (int)Math.Ceiling((double)SchedulePage.TotalCount / SchedulePage.PageSize)
        : 1;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var adminUserId))
        {
            _logger.LogWarning("ScheduleManagement request unauthorized due to invalid NameIdentifier claim.");
            ErrorMessage = "Invalid admin session.";
            return Unauthorized();
        }

        var normalizedPage = Page < 1 ? 1 : Page;
        var normalizedPageSize = PageSize < MinPageSize
            ? MinPageSize
            : PageSize > MaxPageSize
                ? MaxPageSize
                : PageSize;

        if (normalizedPage != Page || normalizedPageSize != PageSize)
        {
            _logger.LogWarning(
                "ScheduleManagement paging normalized. AdminUserId={AdminUserId}, RequestedPage={RequestedPage}, RequestedPageSize={RequestedPageSize}, Page={Page}, PageSize={PageSize}",
                adminUserId,
                Page,
                PageSize,
                normalizedPage,
                normalizedPageSize);
        }

        Page = normalizedPage;
        PageSize = normalizedPageSize;

        var normalizedStatus = string.IsNullOrWhiteSpace(Status) ? null : Status.Trim();

        var filterRequest = new AdminScheduleFilterRequest
        {
            Page = Page,
            PageSize = PageSize,
            FromUtc = From?.ToUniversalTime(),
            ToUtc = To?.ToUniversalTime(),
            SemesterId = SemesterId,
            ClassSectionId = ClassSectionId,
            TeacherId = TeacherId,
            Status = normalizedStatus
        };

        _logger.LogInformation(
            "ScheduleManagement list request. AdminUserId={AdminUserId}, Page={Page}, PageSize={PageSize}, From={From}, To={To}, SemesterId={SemesterId}, ClassSectionId={ClassSectionId}, TeacherId={TeacherId}, Status={Status}",
            adminUserId,
            filterRequest.Page,
            filterRequest.PageSize,
            filterRequest.FromUtc,
            filterRequest.ToUtc,
            filterRequest.SemesterId,
            filterRequest.ClassSectionId,
            filterRequest.TeacherId,
            filterRequest.Status);

        try
        {
            var result = await _adminScheduleService.GetSchedulesAsync(adminUserId, filterRequest, cancellationToken);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError("", result.Message ?? "Failed to load schedules.");
                _logger.LogWarning(
                    "GetSchedulesAsync failed. AdminUserId={AdminUserId}, ErrorCode={ErrorCode}, Message={Message}",
                    adminUserId,
                    result.ErrorCode,
                    result.Message);
                return new PageResult();
            }

            SchedulePage = result.Data ?? new AdminSchedulePageDto
            {
                Page = Page,
                PageSize = PageSize,
                TotalCount = 0,
                Items = []
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in ScheduleManagement list request. AdminUserId={AdminUserId}", adminUserId);
            ErrorMessage = "An error occurred while loading schedules.";
            ModelState.AddModelError("", ErrorMessage);
        }

        // TODO(manual-test): Login as admin -> /Admin/ScheduleManagement/Index and test query filters/page/pageSize.
        return new PageResult();
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;

        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (claim is null)
        {
            return false;
        }

        return int.TryParse(claim.Value, out userId) && userId > 0;
    }
}
