using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BusinessLogic.DTOs.Requests.GradeAppeals;
using BusinessLogic.DTOs.Responses.Gradebook;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Student.Pages.GradeAppeals;

[Authorize(Roles = nameof(UserRole.STUDENT))]
public class CreateModel : PageModel
{
    private readonly IGradeAppealService _appealService;
    private readonly IEnrollmentService _enrollmentService;
    private readonly IGradebookService _gradebookService;
    private readonly ILogger<CreateModel> _logger;

    public CreateModel(
        IGradeAppealService appealService,
        IEnrollmentService enrollmentService,
        IGradebookService gradebookService,
        ILogger<CreateModel> logger)
    {
        _appealService = appealService;
        _enrollmentService = enrollmentService;
        _gradebookService = gradebookService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public int? EnrollmentId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? GradeBookId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? SemesterId { get; set; }

    public string CourseCode { get; set; } = string.Empty;
    public string CourseName { get; set; } = string.Empty;
    public string SectionCode { get; set; } = string.Empty;
    public List<GradeItemOptionViewModel> GradeItemOptions { get; set; } = [];

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        if (!EnrollmentId.HasValue || !GradeBookId.HasValue)
        {
            return RedirectToPage("/GradeAppeals/Index", new { area = "Student", SemesterId });
        }

        var loaded = await LoadAppealContextAsync(userId, EnrollmentId.Value, GradeBookId.Value, ct);
        if (!loaded)
        {
            TempData[nameof(IndexModel.ErrorMessage)] = "Không thể khởi tạo khiếu nại cho môn học này.";
            return RedirectToPage("/GradeAppeals/Index", new { area = "Student", SemesterId });
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        var loaded = await LoadAppealContextAsync(userId, Input.EnrollmentId, Input.GradeBookId, ct);
        if (!loaded)
        {
            ModelState.AddModelError(string.Empty, "Không thể tải thông tin môn học để gửi khiếu nại.");
            return Page();
        }

        if (Input.SelectedGradeItemIds.Count > 0)
        {
            var validGradeItemIds = GradeItemOptions.Select(x => x.GradeItemId).ToHashSet();
            var hasInvalidItem = Input.SelectedGradeItemIds.Any(x => !validGradeItemIds.Contains(x));
            if (hasInvalidItem)
            {
                ModelState.AddModelError(nameof(Input.SelectedGradeItemIds), "Grade item không hợp lệ.");
            }
        }

        if (!ModelState.IsValid) return Page();

        var normalizedEvidenceNote = string.IsNullOrWhiteSpace(Input.EvidenceNote)
            ? null
            : Input.EvidenceNote.Trim();

        int? gradeItemId = null;
        if (Input.SelectedGradeItemIds.Count == 1)
        {
            gradeItemId = Input.SelectedGradeItemIds[0];
        }
        else if (Input.SelectedGradeItemIds.Count > 1)
        {
            var selectedItemDescriptions = GradeItemOptions
                .Where(x => Input.SelectedGradeItemIds.Contains(x.GradeItemId))
                .OrderBy(x => x.SortOrder)
                .Select(x =>
                {
                    var currentScore = x.CurrentScore?.ToString("0.##") ?? "—";
                    return $"{x.ItemName} ({currentScore}/{x.MaxScore:0.##})";
                })
                .ToList();

            var selectedItemsNote = $"Grade items khiếu nại: {string.Join(", ", selectedItemDescriptions)}";
            normalizedEvidenceNote = string.IsNullOrWhiteSpace(normalizedEvidenceNote)
                ? selectedItemsNote
                : $"{normalizedEvidenceNote}\n{selectedItemsNote}";
        }

        try
        {
            var request = new SubmitGradeAppealRequest
            {
                EnrollmentId = Input.EnrollmentId,
                GradeBookId = Input.GradeBookId,
                GradeItemId = gradeItemId,
                AppealContent = Input.AppealContent,
                EvidenceNote = normalizedEvidenceNote
            };

            var result = await _appealService.SubmitAppealAsync(userId, request, ct);
            if (result.IsSuccess && result.Data is not null)
            {
                return RedirectToPage("/GradeAppeals/Detail", new { area = "Student", id = result.Data.AppealId });
            }

            ModelState.AddModelError(string.Empty, result.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting grade appeal");
            ModelState.AddModelError(string.Empty, "Đã xảy ra lỗi khi gửi khiếu nại.");
        }

        return Page();
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var id) ? id : 0;
    }

    private async Task<bool> LoadAppealContextAsync(int userId, int enrollmentId, int gradeBookId, CancellationToken ct)
    {
        var courses = await _enrollmentService.GetMyCoursesAsync(userId, SemesterId, 1, 100);
        var selectedCourse = courses.Items.FirstOrDefault(x => x.EnrollmentId == enrollmentId);
        if (selectedCourse is null)
        {
            return false;
        }

        var gradebookResult = await _gradebookService.GetGradebookAsync(userId, nameof(UserRole.STUDENT), selectedCourse.ClassSectionId, ct);
        if (!gradebookResult.IsSuccess || gradebookResult.Data is null)
        {
            return false;
        }

        var gradebook = gradebookResult.Data;
        if (gradebook.GradeBookId != gradeBookId)
        {
            return false;
        }

        if (!string.Equals(gradebook.Status, GradeBookStatus.PUBLISHED.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        CourseCode = selectedCourse.CourseCode;
        CourseName = selectedCourse.CourseName;
        SectionCode = selectedCourse.SectionCode;

        var scoreByGradeItemId = gradebook.GradeEntries
            .Where(x => x.EnrollmentId == enrollmentId)
            .ToDictionary(x => x.GradeItemId, x => x.Score);

        GradeItemOptions = gradebook.GradeItems
            .OrderBy(x => x.SortOrder)
            .Select(x => new GradeItemOptionViewModel
            {
                GradeItemId = x.GradeItemId,
                ItemName = x.ItemName,
                SortOrder = x.SortOrder,
                MaxScore = x.MaxScore,
                Weight = x.Weight,
                CurrentScore = scoreByGradeItemId.TryGetValue(x.GradeItemId, out var score) ? score : null
            })
            .ToList();

        Input.EnrollmentId = enrollmentId;
        Input.GradeBookId = gradeBookId;

        return true;
    }

    public sealed class GradeItemOptionViewModel
    {
        public int GradeItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public decimal MaxScore { get; set; }
        public decimal? Weight { get; set; }
        public decimal? CurrentScore { get; set; }
    }

    public sealed class InputModel
    {
        public int EnrollmentId { get; set; }

        public int GradeBookId { get; set; }

        [Display(Name = "Grade Item (tuỳ chọn)")]
        public List<int> SelectedGradeItemIds { get; set; } = [];

        [Required(ErrorMessage = "Nội dung khiếu nại là bắt buộc.")]
        [StringLength(1000, ErrorMessage = "Nội dung không được vượt quá 1000 ký tự.")]
        [Display(Name = "Nội dung khiếu nại")]
        public string AppealContent { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Ghi chú bằng chứng không được vượt quá 500 ký tự.")]
        [Display(Name = "Ghi chú bằng chứng")]
        public string? EvidenceNote { get; set; }
    }
}
