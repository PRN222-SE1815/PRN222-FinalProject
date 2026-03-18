using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BusinessLogic.DTOs.GradeAppeals;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Teacher.Pages.GradeAppeals;

[Authorize(Roles = nameof(UserRole.TEACHER))]
public class ReviewModel : PageModel
{
    private readonly IGradeAppealService _appealService;
    private readonly IGradebookService _gradebookService;
    private readonly ILogger<ReviewModel> _logger;

    public ReviewModel(IGradeAppealService appealService, IGradebookService gradebookService, ILogger<ReviewModel> logger)
    {
        _appealService = appealService;
        _gradebookService = gradebookService;
        _logger = logger;
    }

    public GradeAppealDetailDto? Appeal { get; set; }

    [BindProperty]
    public ResolveInputModel ResolveInput { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public decimal? PrefillNewScore { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(long id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        await LoadAppealAsync(userId, id, ct);
        if (Appeal is null)
        {
            ErrorMessage ??= "Không tìm thấy khiếu nại.";
            return RedirectToPage("/GradeAppeals/Queue", new { area = "Teacher" });
        }

        ResolveInput.AppealId = Appeal.AppealId;
        if (PrefillNewScore.HasValue)
        {
            ResolveInput.NewScore = PrefillNewScore;
            ResolveInput.Outcome = GradeAppealStatus.Approved;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostStartReviewAsync(long id, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        try
        {
            var result = await _appealService.StartReviewAsync(userId, id, ct);
            if (result.IsSuccess)
            {
                SuccessMessage = "Đã bắt đầu xem xét khiếu nại.";
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error starting review for appeal {AppealId}", id);
            ErrorMessage = "Đã xảy ra lỗi.";
        }

        return RedirectToPage("/GradeAppeals/Review", new { area = "Teacher", id });
    }

    public async Task<IActionResult> OnPostResolveAsync(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        await LoadAppealAsync(userId, ResolveInput.AppealId, ct);
        if (Appeal is null)
        {
            ErrorMessage = "Không thể tải thông tin khiếu nại.";
            return RedirectToPage("/GradeAppeals/Queue", new { area = "Teacher" });
        }

        if (string.Equals(ResolveInput.Outcome, GradeAppealStatus.Approved, StringComparison.OrdinalIgnoreCase)
            && ResolveInput.NewScore.HasValue)
        {
            if (!Appeal.GradeItemId.HasValue)
            {
                ModelState.AddModelError(nameof(ResolveInput.NewScore), "Khiếu nại không gắn với một Grade Item cụ thể nên không thể nhập điểm mới tại đây.");
            }
            else
            {
                var gradebookResult = await _gradebookService.GetGradebookAsync(userId, nameof(UserRole.TEACHER), Appeal.ClassSectionId, ct);
                if (!gradebookResult.IsSuccess || gradebookResult.Data is null)
                {
                    ModelState.AddModelError(string.Empty, gradebookResult.Message);
                }
                else
                {
                    var gradeEntry = gradebookResult.Data.GradeEntries.FirstOrDefault(x =>
                        x.EnrollmentId == Appeal.EnrollmentId
                        && x.GradeItemId == Appeal.GradeItemId.Value);

                    if (gradeEntry is null)
                    {
                        ModelState.AddModelError(nameof(ResolveInput.NewScore), "Không tìm thấy Grade Entry tương ứng để cập nhật điểm.");
                    }
                    else
                    {
                        ResolveInput.GradeEntryId = gradeEntry.GradeEntryId;
                    }
                }
            }
        }
        else
        {
            ResolveInput.NewScore = null;
            ResolveInput.GradeEntryId = null;
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var request = new ResolveGradeAppealRequest
            {
                AppealId = ResolveInput.AppealId,
                Outcome = ResolveInput.Outcome,
                ResponseMessage = ResolveInput.ResponseMessage,
                GradeEntryId = ResolveInput.GradeEntryId,
                NewScore = ResolveInput.NewScore
            };

            var result = await _appealService.ResolveAppealAsync(userId, request, ct);
            if (result.IsSuccess)
            {
                SuccessMessage = "Khiếu nại đã được xử lý thành công.";
                return RedirectToPage("/GradeAppeals/Queue", new { area = "Teacher" });
            }

            ErrorMessage = result.Message;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resolving appeal {AppealId}", ResolveInput.AppealId);
            ErrorMessage = "Đã xảy ra lỗi khi xử lý khiếu nại.";
        }

        await LoadAppealAsync(userId, ResolveInput.AppealId, ct);
        return Page();
    }

    private async Task LoadAppealAsync(int userId, long appealId, CancellationToken ct)
    {
        try
        {
            var result = await _appealService.GetDetailAsync(userId, appealId, ct);
            if (result.IsSuccess && result.Data is not null)
            {
                Appeal = result.Data;
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading appeal detail {AppealId}", appealId);
            ErrorMessage = "Đã xảy ra lỗi khi tải chi tiết khiếu nại.";
        }
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var id) ? id : 0;
    }

    public sealed class ResolveInputModel
    {
        public long AppealId { get; set; }

        [Required(ErrorMessage = "Kết quả xử lý là bắt buộc.")]
        [Display(Name = "Kết quả")]
        public string Outcome { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phản hồi là bắt buộc.")]
        [StringLength(1000, ErrorMessage = "Phản hồi không được vượt quá 1000 ký tự.")]
        [Display(Name = "Phản hồi")]
        public string ResponseMessage { get; set; } = string.Empty;

        [Display(Name = "Grade Entry ID (tuỳ chọn)")]
        public int? GradeEntryId { get; set; }

        [Display(Name = "Điểm mới (tuỳ chọn)")]
        public decimal? NewScore { get; set; }
    }
}
