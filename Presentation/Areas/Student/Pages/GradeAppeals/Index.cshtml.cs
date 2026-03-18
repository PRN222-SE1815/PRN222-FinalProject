using System.Security.Claims;
using BusinessLogic.DTOs.GradeAppeals;
using BusinessLogic.DTOs.Response;
using BusinessLogic.DTOs.Responses;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Student.Pages.GradeAppeals;

[Authorize(Roles = nameof(UserRole.STUDENT))]
public class IndexModel : PageModel
{
    private readonly IGradeAppealService _appealService;
    private readonly IEnrollmentService _enrollmentService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IGradeAppealService appealService, IEnrollmentService enrollmentService, ILogger<IndexModel> logger)
    {
        _appealService = appealService;
        _enrollmentService = enrollmentService;
        _logger = logger;
    }

    public PagedResult<GradeAppealListItemDto> Result { get; set; } = new();
    public List<SemesterOptionDto> Semesters { get; set; } = [];
    public int? SelectedSemesterId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

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
            if (!SemesterId.HasValue)
            {
                var tempPage = await _enrollmentService.GetMyCoursesAsync(userId, null, 1, 1);
                var activeSem = tempPage.Semesters.FirstOrDefault(s => s.IsActive);
                if (activeSem is not null)
                {
                    SemesterId = activeSem.SemesterId;
                }
            }

            var semesterPage = await _enrollmentService.GetMyCoursesAsync(userId, SemesterId, 1, 1);
            Semesters = semesterPage.Semesters.ToList();
            SelectedSemesterId = SemesterId ?? semesterPage.Semesters.FirstOrDefault(s => s.IsActive)?.SemesterId;

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
            _logger.LogError(ex, "Error loading student grade appeals");
            ErrorMessage = "Đã xảy ra lỗi khi tải danh sách khiếu nại.";
        }

        return Page();
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var id) ? id : 0;
    }
}
