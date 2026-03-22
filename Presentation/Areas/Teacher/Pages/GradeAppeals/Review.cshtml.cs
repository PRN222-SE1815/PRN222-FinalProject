using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BusinessLogic.DTOs.Requests.GradeAppeals;
using BusinessLogic.DTOs.Responses.GradeAppeals;
using BusinessLogic.DTOs.Responses.Gradebook;
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
    public List<AppealedGradeItemViewModel> AppealedGradeItems { get; set; } = [];

    [BindProperty]
    public ResolveInputModel ResolveInput { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public List<PrefillScoreInputModel> PrefillScoreUpdates { get; set; } = [];

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

        await LoadAppealedGradeItemsAsync(userId, ct);

        ResolveInput.AppealId = Appeal.AppealId;
        InitializeResolveScoreUpdates();

        if (PrefillScoreUpdates.Count > 0)
        {
            ResolveInput.Outcome = GradeAppealStatus.Approved;

            var prefillByGradeItemId = PrefillScoreUpdates
                .Where(x => x.GradeItemId > 0)
                .ToDictionary(x => x.GradeItemId, x => x.NewScore);

            foreach (var row in ResolveInput.ScoreUpdates)
            {
                if (prefillByGradeItemId.TryGetValue(row.GradeItemId, out var newScore))
                {
                    row.NewScore = newScore;
                }
            }
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

        await LoadAppealedGradeItemsAsync(userId, ct);
        InitializeResolveScoreUpdates();

        var scoreChanges = new List<ResolveGradeAppealScoreChangeRequest>();
        if (string.Equals(ResolveInput.Outcome, GradeAppealStatus.Approved, StringComparison.OrdinalIgnoreCase))
        {
            if (AppealedGradeItems.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Không tìm thấy Grade Item khả dụng để cập nhật điểm.");
            }
            else
            {
                foreach (var row in ResolveInput.ScoreUpdates)
                {
                    if (!row.NewScore.HasValue)
                    {
                        continue;
                    }

                    var selectedItem = AppealedGradeItems.FirstOrDefault(x => x.GradeItemId == row.GradeItemId);

                    if (selectedItem is null)
                    {
                        ModelState.AddModelError(string.Empty, "Grade Item được chọn không hợp lệ.");
                    }
                    else if (!selectedItem.GradeEntryId.HasValue)
                    {
                        ModelState.AddModelError(string.Empty, $"Không tìm thấy Grade Entry cho {selectedItem.ItemName}.");
                    }
                    else if (row.NewScore.Value < 0 || row.NewScore.Value > selectedItem.MaxScore)
                    {
                        ModelState.AddModelError($"ResolveInput.ScoreUpdates[{row.Index}].NewScore", $"Điểm của {selectedItem.ItemName} phải nằm trong khoảng từ 0 đến {selectedItem.MaxScore:0.##}.");
                    }
                    else
                    {
                        row.GradeEntryId = selectedItem.GradeEntryId;
                        scoreChanges.Add(new ResolveGradeAppealScoreChangeRequest
                        {
                            GradeEntryId = selectedItem.GradeEntryId.Value,
                            NewScore = row.NewScore.Value
                        });
                    }
                }
            }
        }
        else
        {
            var hasAnyScoreInput = ResolveInput.ScoreUpdates.Any(x => x.NewScore.HasValue);
            if (hasAnyScoreInput)
            {
                ModelState.AddModelError(string.Empty, "Chỉ được nhập điểm mới khi kết quả là Chấp nhận.");
            }

            ResolveInput.ScoreUpdates.ForEach(x => x.NewScore = null);
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
                ScoreChanges = scoreChanges
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
        await LoadAppealedGradeItemsAsync(userId, ct);
        InitializeResolveScoreUpdates();
        return Page();
    }

    private void InitializeResolveScoreUpdates()
    {
        var existingByGradeItemId = ResolveInput.ScoreUpdates
            .Where(x => x.GradeItemId > 0)
            .ToDictionary(x => x.GradeItemId, x => x);

        ResolveInput.ScoreUpdates = AppealedGradeItems
            .Select((x, idx) =>
            {
                existingByGradeItemId.TryGetValue(x.GradeItemId, out var existing);
                return new ResolveScoreUpdateInputModel
                {
                    Index = idx,
                    GradeItemId = x.GradeItemId,
                    GradeEntryId = x.GradeEntryId,
                    GradeItemName = x.ItemName,
                    CurrentScore = x.CurrentScore,
                    MaxScore = x.MaxScore,
                    NewScore = existing?.NewScore
                };
            })
            .ToList();
    }

    private async Task LoadAppealedGradeItemsAsync(int userId, CancellationToken ct)
    {
        AppealedGradeItems = [];
        if (Appeal is null)
        {
            return;
        }

        var gradebookResult = await _gradebookService.GetGradebookAsync(userId, nameof(UserRole.TEACHER), Appeal.ClassSectionId, ct);
        if (!gradebookResult.IsSuccess || gradebookResult.Data is null)
        {
            if (!string.IsNullOrWhiteSpace(gradebookResult.Message))
            {
                ErrorMessage ??= gradebookResult.Message;
            }
            return;
        }

        var gradebook = gradebookResult.Data;
        var entryByGradeItemId = gradebook.GradeEntries
            .Where(x => x.EnrollmentId == Appeal.EnrollmentId)
            .ToDictionary(x => x.GradeItemId, x => x);

        IEnumerable<GradeItemResponse> targetItems;

        if (Appeal.GradeItemId.HasValue)
        {
            targetItems = gradebook.GradeItems.Where(x => x.GradeItemId == Appeal.GradeItemId.Value);
        }
        else
        {
            var appealedItemNames = ParseAppealedItemNames(Appeal.EvidenceNote);
            targetItems = appealedItemNames.Count == 0
                ? gradebook.GradeItems
                : gradebook.GradeItems.Where(x => appealedItemNames.Contains(x.ItemName));
        }

        AppealedGradeItems = targetItems
            .OrderBy(x => x.SortOrder)
            .Select(x => new AppealedGradeItemViewModel
            {
                GradeItemId = x.GradeItemId,
                ItemName = x.ItemName,
                MaxScore = x.MaxScore,
                CurrentScore = entryByGradeItemId.TryGetValue(x.GradeItemId, out var entry) ? entry.Score : null,
                GradeEntryId = entryByGradeItemId.TryGetValue(x.GradeItemId, out var selectedEntry) ? selectedEntry.GradeEntryId : null
            })
            .ToList();
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

    private static HashSet<string> ParseAppealedItemNames(string? evidenceNote)
    {
        if (string.IsNullOrWhiteSpace(evidenceNote))
        {
            return [];
        }

        const string prefix = "Grade items khiếu nại:";
        var lines = evidenceNote
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var targetLine = lines.FirstOrDefault(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(targetLine))
        {
            return [];
        }

        var rawItems = targetLine[prefix.Length..]
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rawItem in rawItems)
        {
            var itemName = rawItem;
            var scoreHintIndex = rawItem.IndexOf('(');
            if (scoreHintIndex >= 0)
            {
                itemName = rawItem[..scoreHintIndex].Trim();
            }

            if (!string.IsNullOrWhiteSpace(itemName))
            {
                result.Add(itemName);
            }
        }

        return result;
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

        public List<ResolveScoreUpdateInputModel> ScoreUpdates { get; set; } = [];
    }

    public sealed class AppealedGradeItemViewModel
    {
        public int GradeItemId { get; set; }
        public int? GradeEntryId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal? CurrentScore { get; set; }
        public decimal MaxScore { get; set; }
    }

    public sealed class ResolveScoreUpdateInputModel
    {
        public int Index { get; set; }
        public int GradeItemId { get; set; }
        public int? GradeEntryId { get; set; }
        public string GradeItemName { get; set; } = string.Empty;
        public decimal? CurrentScore { get; set; }
        public decimal MaxScore { get; set; }
        public decimal? NewScore { get; set; }
    }

    public sealed class PrefillScoreInputModel
    {
        public int GradeItemId { get; set; }
        public decimal? NewScore { get; set; }
    }
}
