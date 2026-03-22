using System.Security.Claims;
using BusinessLogic.DTOs.Requests.GradeAppeals;
using BusinessLogic.DTOs.Responses;
using BusinessLogic.DTOs.Responses.GradeAppeals;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Teacher.Pages.GradeAppeals;

[Authorize(Roles = nameof(UserRole.TEACHER))]
public class QueueModel : PageModel
{
    private readonly IGradeAppealService _appealService;
    private readonly ILogger<QueueModel> _logger;

    public QueueModel(IGradeAppealService appealService, ILogger<QueueModel> logger)
    {
        _appealService = appealService;
        _logger = logger;
    }

    public PagedResult<GradeAppealListItemDto> Result { get; set; } = new();
    public List<SemesterOptionDto> Semesters { get; set; } = [];
    public int? SelectedSemesterId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; } = "SUBMITTED";

    [BindProperty(SupportsGet = true)]
    public int? SemesterId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 10;

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        try
        {
            var semesterOptions = await _appealService.GetSemesterOptionsAsync(ct);
            Semesters = semesterOptions.ToList();

            if (!SemesterId.HasValue)
            {
                SemesterId = Semesters.FirstOrDefault(s => s.IsActive)?.SemesterId
                    ?? Semesters.FirstOrDefault()?.SemesterId;
            }

            SelectedSemesterId = SemesterId;

            var query = new GradeAppealQueryRequest
            {
                SemesterId = SelectedSemesterId,
                Status = Status,
                Page = PageNumber,
                PageSize = PageSize
            };

            var result = await _appealService.GetPagedAsync(userId, query, ct);
            if (result.IsSuccess && result.Data is not null)
            {
                Result = result.Data;
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading teacher grade appeal queue");
            ErrorMessage = "Đã xảy ra lỗi khi tải hàng đợi khiếu nại.";
        }

        return Page();
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var id) ? id : 0;
    }
}
