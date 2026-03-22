using System.Security.Claims;
using BusinessLogic.DTOs.Responses.GradeAppeals;
using BusinessLogic.DTOs.Responses.Gradebook;
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
    private readonly IGradebookService _gradebookService;
    private readonly ILogger<DetailModel> _logger;

    public DetailModel(IGradeAppealService appealService, IGradebookService gradebookService, ILogger<DetailModel> logger)
    {
        _appealService = appealService;
        _gradebookService = gradebookService;
        _logger = logger;
    }

    public GradeAppealDetailDto? Appeal { get; set; }
    public List<AppealedGradeItemViewModel> AppealedGradeItems { get; set; } = [];

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
                await LoadAppealedGradeItemsAsync(userId, ct);
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

    private async Task LoadAppealedGradeItemsAsync(int userId, CancellationToken ct)
    {
        AppealedGradeItems = [];
        if (Appeal is null)
        {
            return;
        }

        var gradebookResult = await _gradebookService.GetGradebookAsync(userId, nameof(UserRole.STUDENT), Appeal.ClassSectionId, ct);
        if (!gradebookResult.IsSuccess || gradebookResult.Data is null)
        {
            if (Appeal.GradeItemId.HasValue)
            {
                AppealedGradeItems =
                [
                    new AppealedGradeItemViewModel
                    {
                        GradeItemId = Appeal.GradeItemId.Value,
                        ItemName = Appeal.GradeItemName ?? Appeal.GradeItemId.Value.ToString(),
                        CurrentScore = Appeal.GradeItemScore,
                        MaxScore = Appeal.GradeItemMaxScore
                    }
                ];
            }

            return;
        }

        var gradebook = gradebookResult.Data;
        var entryByGradeItemId = gradebook.GradeEntries
            .Where(x => x.EnrollmentId == Appeal.EnrollmentId)
            .ToDictionary(x => x.GradeItemId, x => x.Score);

        IEnumerable<GradeItemResponse> targetItems;
        if (Appeal.GradeItemId.HasValue)
        {
            targetItems = gradebook.GradeItems.Where(x => x.GradeItemId == Appeal.GradeItemId.Value);
        }
        else
        {
            var appealedItemNames = ParseAppealedItemNames(Appeal.EvidenceNote);
            targetItems = appealedItemNames.Count == 0
                ? []
                : gradebook.GradeItems.Where(x => appealedItemNames.Contains(x.ItemName));
        }

        AppealedGradeItems = targetItems
            .OrderBy(x => x.SortOrder)
            .Select(x => new AppealedGradeItemViewModel
            {
                GradeItemId = x.GradeItemId,
                ItemName = x.ItemName,
                CurrentScore = entryByGradeItemId.TryGetValue(x.GradeItemId, out var score) ? score : null,
                MaxScore = x.MaxScore
            })
            .ToList();
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

    public sealed class AppealedGradeItemViewModel
    {
        public int GradeItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal? CurrentScore { get; set; }
        public decimal? MaxScore { get; set; }
    }
}
