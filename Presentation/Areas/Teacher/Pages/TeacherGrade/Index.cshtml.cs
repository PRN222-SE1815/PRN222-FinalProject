using System.Security.Claims;
using BusinessLogic.DTOs.Requests.GradeAppeals;
using BusinessLogic.DTOs.Requests.Gradebook;
using BusinessLogic.DTOs.Responses.Gradebook;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Teacher.Pages.TeacherGrade;

[Authorize(Roles = nameof(UserRole.TEACHER))]
public class IndexModel : PageModel
{
    private readonly IGradebookService _gradebookService;
    private readonly IGradeBookExportService _gradeBookExportService;
    private readonly IGradeAppealService _gradeAppealService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IGradebookService gradebookService, IGradeBookExportService gradeBookExportService, IGradeAppealService gradeAppealService, ILogger<IndexModel> logger)
    {
        _gradebookService = gradebookService;
        _gradeBookExportService = gradeBookExportService;
        _gradeAppealService = gradeAppealService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int ClassSectionId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? HighlightEnrollmentId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? HighlightGradeItemId { get; set; }

    [BindProperty(SupportsGet = true)]
    public long? AppealId { get; set; }

    [BindProperty(SupportsGet = true)]
    public decimal? PrefillNewScore { get; set; }

    public GradebookDetailResponse? Gradebook { get; set; }
    public GradeAnalyticsViewModel? GradeAnalytics { get; set; }
    public List<AppealFocusGradeItemViewModel> AppealFocusGradeItems { get; set; } = [];
    public HashSet<int> AppealFocusGradeItemIds => AppealFocusGradeItems.Select(x => x.GradeItemId).ToHashSet();

    public IReadOnlyList<EnrollmentRow> EnrollmentRows { get; set; } = [];

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    [BindProperty]
    public UpsertScoresRequest SaveRequest { get; set; } = new();

    [BindProperty]
    public RequestApprovalRequest ApprovalRequest { get; set; } = new();

    public bool CanEdit => Gradebook is not null
        && (string.Equals(Gradebook.Status, "DRAFT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Gradebook.Status, "REJECTED", StringComparison.OrdinalIgnoreCase));

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        if (ClassSectionId <= 0)
        {
            return RedirectToPage("/MyClasses/Index");
        }

        await LoadGradebookAsync(userId);
        await LoadAppealFocusContextAsync(userId);
        return Page();
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        var result = await _gradebookService.UpsertTeacherScoresAsync(
            userId,
            nameof(UserRole.TEACHER),
            SaveRequest);

        if (result.IsSuccess)
        {
            SuccessMessage = "Scores saved successfully.";
        }
        else
        {
            ErrorMessage = result.Message;
        }

        ClassSectionId = SaveRequest.ClassSectionId;
        return RedirectToPage(new { ClassSectionId });
    }

    public async Task<IActionResult> OnPostSubmitReviewAsync()
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        var result = await _gradebookService.RequestApprovalAsync(
            userId,
            nameof(UserRole.TEACHER),
            ApprovalRequest);

        if (result.IsSuccess)
        {
            SuccessMessage = "Gradebook submitted for review.";
        }
        else
        {
            ErrorMessage = result.Message;
        }

        ClassSectionId = ApprovalRequest.ClassSectionId;
        return RedirectToPage(new { ClassSectionId });
    }

    public async Task<IActionResult> OnPostExportAsync(int classSectionId, string format, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        var result = await _gradeBookExportService.ExportClassSectionAsync(
            userId,
            new ExportGradeBookRequest
            {
                ClassSectionId = classSectionId,
                Format = format
            },
            ct);

        if (!result.IsSuccess || result.Data is null)
        {
            ErrorMessage = result.Message;
            return RedirectToPage(new { ClassSectionId = classSectionId });
        }

        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    private async Task LoadGradebookAsync(int userId)
    {
        var result = await _gradebookService.GetGradebookAsync(
            userId,
            nameof(UserRole.TEACHER),
            ClassSectionId);

        if (result.IsSuccess && result.Data is not null)
        {
            Gradebook = result.Data;
            BuildEnrollmentRows();
            BuildGradeAnalytics();
        }
        else
        {
            ErrorMessage = result.Message;
        }
    }

    private async Task LoadAppealFocusContextAsync(int userId)
    {
        AppealFocusGradeItems = [];
        if (!AppealId.HasValue || !HighlightEnrollmentId.HasValue || Gradebook is null)
        {
            return;
        }

        var appealResult = await _gradeAppealService.GetDetailAsync(userId, AppealId.Value);
        if (!appealResult.IsSuccess || appealResult.Data is null)
        {
            return;
        }

        var appeal = appealResult.Data;
        if (appeal.ClassSectionId != ClassSectionId || appeal.EnrollmentId != HighlightEnrollmentId.Value)
        {
            return;
        }

        var entryByGradeItemId = Gradebook.GradeEntries
            .Where(x => x.EnrollmentId == HighlightEnrollmentId.Value)
            .ToDictionary(x => x.GradeItemId, x => x.Score);

        IEnumerable<GradeItemResponse> targetItems;
        if (appeal.GradeItemId.HasValue)
        {
            targetItems = Gradebook.GradeItems.Where(x => x.GradeItemId == appeal.GradeItemId.Value);
        }
        else
        {
            var appealedItemNames = ParseAppealedItemNames(appeal.EvidenceNote);
            targetItems = appealedItemNames.Count == 0
                ? Gradebook.GradeItems
                : Gradebook.GradeItems.Where(x => appealedItemNames.Contains(x.ItemName));
        }

        AppealFocusGradeItems = targetItems
            .OrderBy(x => x.SortOrder)
            .Select(x => new AppealFocusGradeItemViewModel
            {
                GradeItemId = x.GradeItemId,
                ItemName = x.ItemName,
                CurrentScore = entryByGradeItemId.TryGetValue(x.GradeItemId, out var score) ? score : null,
                MaxScore = x.MaxScore,
                PrefillNewScore = PrefillNewScore
            })
            .ToList();

        if (AppealFocusGradeItems.Count == 1)
        {
            HighlightGradeItemId = AppealFocusGradeItems[0].GradeItemId;
        }
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

    private void BuildEnrollmentRows()
    {
        if (Gradebook is null) return;

        var studentLookup = Gradebook.GradeEntries
            .GroupBy(e => e.EnrollmentId)
            .ToDictionary(g => g.Key, g => g.First());

        var enrollmentIds = studentLookup.Keys.OrderBy(id => id).ToList();

        var entryLookup = Gradebook.GradeEntries
            .GroupBy(e => e.EnrollmentId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.GradeItemId));

        var rows = new List<EnrollmentRow>();
        foreach (var enrollmentId in enrollmentIds)
        {
            var first = studentLookup[enrollmentId];
            var row = new EnrollmentRow
            {
                EnrollmentId = enrollmentId,
                StudentCode = first.StudentCode,
                StudentName = first.StudentName
            };
            entryLookup.TryGetValue(enrollmentId, out var entriesByItem);

            foreach (var item in Gradebook.GradeItems)
            {
                decimal? score = null;
                if (entriesByItem is not null && entriesByItem.TryGetValue(item.GradeItemId, out var entry))
                {
                    score = entry.Score;
                }
                row.Scores[item.GradeItemId] = score;
            }
            rows.Add(row);
        }
        EnrollmentRows = rows;
    }

    private void BuildGradeAnalytics()
    {
        if (Gradebook is null || Gradebook.GradeItems.Count == 0 || EnrollmentRows.Count == 0)
        {
            GradeAnalytics = null;
            return;
        }

        var orderedItems = Gradebook.GradeItems
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.GradeItemId)
            .ToList();

        var studentTotals = EnrollmentRows
            .Select(row => new StudentPerformanceRowViewModel
            {
                EnrollmentId = row.EnrollmentId,
                StudentCode = row.StudentCode,
                StudentName = row.StudentName,
                AverageScore = CalculateStudentTotalScore(row, orderedItems)
            })
            .ToList();

        var distributionLabels = new List<string>
        {
            "< 4.0",
            "4.0 - 5.49",
            "5.5 - 6.99",
            "7.0 - 8.49",
            "8.5 - 10"
        };
        var distributionValues = new int[distributionLabels.Count];

        foreach (var total in studentTotals.Select(x => x.AverageScore))
        {
            distributionValues[ResolveScoreBand(total)]++;
        }

        var itemLabels = new List<string>();
        var itemAverageValues = new List<decimal>();

        foreach (var item in orderedItems)
        {
            var scores = EnrollmentRows
                .Select(row => row.Scores.TryGetValue(item.GradeItemId, out var score) ? score : null)
                .Where(score => score.HasValue)
                .Select(score => score!.Value)
                .ToList();

            var averageNormalized = 0m;
            if (scores.Count > 0 && item.MaxScore > 0m)
            {
                var averageRaw = scores.Average();
                averageNormalized = Math.Clamp((averageRaw / item.MaxScore) * 10m, 0m, 10m);
            }

            itemLabels.Add(item.ItemName);
            itemAverageValues.Add(Math.Round(averageNormalized, 2, MidpointRounding.AwayFromZero));
        }

        GradeAnalytics = new GradeAnalyticsViewModel
        {
            DistributionLabels = distributionLabels,
            DistributionValues = distributionValues.ToList(),
            PerformanceLabels = itemLabels,
            PerformanceValues = itemAverageValues,
            RankingLabels = new List<string>
            {
                "Yếu (< 4.0)",
                "Trung bình (4.0 - 5.49)",
                "Khá (5.5 - 6.99)",
                "Giỏi (7.0 - 8.49)",
                "Xuất sắc (8.5 - 10)"
            },
            RankingValues = new List<int>
            {
                distributionValues[0],
                distributionValues[1],
                distributionValues[2],
                distributionValues[3],
                distributionValues[4]
            },
            TopStudents = studentTotals
                .OrderByDescending(x => x.AverageScore)
                .ThenBy(x => x.StudentName)
                .ThenBy(x => x.StudentCode)
                .Take(3)
                .ToList(),
            BottomStudents = studentTotals
                .OrderBy(x => x.AverageScore)
                .ThenBy(x => x.StudentName)
                .ThenBy(x => x.StudentCode)
                .Take(3)
                .ToList()
        };
    }

    private static decimal CalculateStudentTotalScore(EnrollmentRow row, IReadOnlyList<GradeItemResponse> items)
    {
        var weightedItems = items.Where(x => x.Weight.HasValue && x.Weight.Value > 0m).ToList();
        if (weightedItems.Count > 0)
        {
            var weightedTotal = 0m;
            foreach (var item in weightedItems)
            {
                row.Scores.TryGetValue(item.GradeItemId, out var score);
                var normalizedScore = score.HasValue
                    ? NormalizeToTen(score.Value, item.MaxScore)
                    : 0m;

                weightedTotal += normalizedScore * item.Weight!.Value;
            }

            return Math.Round(Math.Clamp(weightedTotal, 0m, 10m), 2, MidpointRounding.AwayFromZero);
        }

        var normalizedScores = items
            .Select(item => row.Scores.TryGetValue(item.GradeItemId, out var score) && score.HasValue
                ? NormalizeToTen(score.Value, item.MaxScore)
                : (decimal?)null)
            .Where(x => x.HasValue)
            .Select(x => x!.Value)
            .ToList();

        if (normalizedScores.Count == 0)
        {
            return 0m;
        }

        return Math.Round(normalizedScores.Average(), 2, MidpointRounding.AwayFromZero);
    }

    private static decimal NormalizeToTen(decimal score, decimal maxScore)
    {
        if (maxScore <= 0m)
        {
            return 0m;
        }

        return Math.Clamp((score / maxScore) * 10m, 0m, 10m);
    }

    private static int ResolveScoreBand(decimal total)
    {
        if (total < 4.0m)
        {
            return 0;
        }

        if (total < 5.5m)
        {
            return 1;
        }

        if (total < 7.0m)
        {
            return 2;
        }

        if (total < 8.5m)
        {
            return 3;
        }

        return 4;
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var id) ? id : 0;
    }

    public sealed class EnrollmentRow
    {
        public int EnrollmentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public Dictionary<int, decimal?> Scores { get; set; } = new();
    }

    public sealed class AppealFocusGradeItemViewModel
    {
        public int GradeItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public decimal? CurrentScore { get; set; }
        public decimal MaxScore { get; set; }
        public decimal? PrefillNewScore { get; set; }
    }

    public sealed class GradeAnalyticsViewModel
    {
        public IReadOnlyList<string> DistributionLabels { get; set; } = [];
        public IReadOnlyList<int> DistributionValues { get; set; } = [];
        public IReadOnlyList<string> PerformanceLabels { get; set; } = [];
        public IReadOnlyList<decimal> PerformanceValues { get; set; } = [];
        public IReadOnlyList<string> RankingLabels { get; set; } = [];
        public IReadOnlyList<int> RankingValues { get; set; } = [];
        public IReadOnlyList<StudentPerformanceRowViewModel> TopStudents { get; set; } = [];
        public IReadOnlyList<StudentPerformanceRowViewModel> BottomStudents { get; set; } = [];
    }

    public sealed class StudentPerformanceRowViewModel
    {
        public int EnrollmentId { get; set; }
        public string StudentCode { get; set; } = string.Empty;
        public string StudentName { get; set; } = string.Empty;
        public decimal AverageScore { get; set; }
    }
}
