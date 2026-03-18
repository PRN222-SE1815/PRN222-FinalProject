using System.Security.Claims;
using BusinessLogic.DTOs.Requests.Gradebook;
using BusinessLogic.DTOs.Responses.Gradebook;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Admin.Pages.GradebookManagement;

[Authorize(Roles = nameof(UserRole.ADMIN))]
public class ReviewModel : PageModel
{
    private readonly IGradebookService _gradebookService;
    private readonly IGradeBookExportService _gradeBookExportService;
    private readonly ILogger<ReviewModel> _logger;

    public ReviewModel(IGradebookService gradebookService, IGradeBookExportService gradeBookExportService, ILogger<ReviewModel> logger)
    {
        _gradebookService = gradebookService;
        _gradeBookExportService = gradeBookExportService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int ClassSectionId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? HighlightEnrollmentId { get; set; }

    public GradebookDetailResponse? Gradebook { get; set; }

    public IReadOnlyList<EnrollmentRow> EnrollmentRows { get; set; } = [];

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    [BindProperty]
    public string? RejectReason { get; set; }

    [BindProperty]
    public string? ApproveMessage { get; set; }

    public bool IsPendingApproval => Gradebook is not null
        && string.Equals(Gradebook.Status, "PENDING_APPROVAL", StringComparison.OrdinalIgnoreCase);

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        if (ClassSectionId <= 0)
        {
            ErrorMessage = "Invalid class section.";
            return RedirectToPage("Index");
        }

        await LoadGradebookAsync(userId);
        return Page();
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
            return RedirectToPage(new { classSectionId });
        }

        return File(result.Data.Content, result.Data.ContentType, result.Data.FileName);
    }

    public async Task<IActionResult> OnPostApproveAsync()
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        var result = await _gradebookService.ApproveGradebookAsync(
            userId,
            nameof(UserRole.ADMIN),
            new ApproveGradebookRequest
            {
                ClassSectionId = ClassSectionId,
                ResponseMessage = ApproveMessage
            });

        if (result.IsSuccess)
        {
            SuccessMessage = "Gradebook approved and published successfully.";
        }
        else
        {
            ErrorMessage = result.Message;
        }

        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnPostRejectAsync()
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        if (string.IsNullOrWhiteSpace(RejectReason))
        {
            ErrorMessage = "Rejection reason is required.";
            await LoadGradebookAsync(userId);
            return Page();
        }

        var result = await _gradebookService.RejectGradebookAsync(
            userId,
            nameof(UserRole.ADMIN),
            new RejectGradebookRequest
            {
                ClassSectionId = ClassSectionId,
                ResponseMessage = RejectReason
            });

        if (result.IsSuccess)
        {
            SuccessMessage = "Gradebook rejected.";
        }
        else
        {
            ErrorMessage = result.Message;
        }

        return RedirectToPage("Index");
    }

    private async Task LoadGradebookAsync(int userId)
    {
        var result = await _gradebookService.GetGradebookAsync(
            userId,
            nameof(UserRole.ADMIN),
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

        var enrollmentIds = Gradebook.GradeEntries
            .Select(e => e.EnrollmentId)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        var entryLookup = Gradebook.GradeEntries
            .GroupBy(e => e.EnrollmentId)
            .ToDictionary(g => g.Key, g => g.ToDictionary(x => x.GradeItemId));

        var rows = new List<EnrollmentRow>();
        foreach (var enrollmentId in enrollmentIds)
        {
            var row = new EnrollmentRow { EnrollmentId = enrollmentId };
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
        public Dictionary<int, decimal?> Scores { get; set; } = new();
    }
}
