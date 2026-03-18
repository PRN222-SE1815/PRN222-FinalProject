using System.Security.Claims;
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
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IGradebookService gradebookService, IGradeBookExportService gradeBookExportService, ILogger<IndexModel> logger)
    {
        _gradebookService = gradebookService;
        _gradeBookExportService = gradeBookExportService;
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
        }
        else
        {
            ErrorMessage = result.Message;
        }
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
}
