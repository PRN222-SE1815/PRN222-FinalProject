using System.Security.Claims;
using BusinessLogic.DTOs.Requests.CourseManagement;
using BusinessLogic.DTOs.Response;
using BusinessLogic.DTOs.Responses.CourseManagement;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Admin.Pages.CourseManagement;

[Authorize(Roles = nameof(UserRole.ADMIN))]
public class IndexModel : PageModel
{
    private readonly ICourseManagementService _courseManagementService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        ICourseManagementService courseManagementService,
        ILogger<IndexModel> logger)
    {
        _courseManagementService = courseManagementService;
        _logger = logger;
    }

    [BindProperty(SupportsGet = true)]
    public string? Keyword { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool? IsActive { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? SemesterId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 20;

    [BindProperty]
    public CreateCourseRequest CreateRequest { get; set; } = new();

    [BindProperty]
    public UpdateCourseRequest UpdateRequest { get; set; } = new();

    [BindProperty]
    public DeactivateCourseRequest DeactivateRequest { get; set; } = new();

    public PagedResultDto? CoursePage { get; set; }

    public CourseDetailResponse? EditCourse { get; set; }

    public List<SemesterOption> Semesters { get; set; } = [];

    /// <summary>Semesters strictly after the current active semester (for Create Course).</summary>
    public List<SemesterOption> FutureSemesters { get; set; } = [];

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        try
        {
            var semResult = await _courseManagementService.GetAllSemesterOptionsAsync(userId, nameof(UserRole.ADMIN));
            if (semResult.IsSuccess && semResult.Data is not null)
            {
                var allSemesters = semResult.Data;
                Semesters = allSemesters
                    .Select(s => new SemesterOption { SemesterId = s.SemesterId, SemesterName = s.SemesterName, IsActive = s.IsActive })
                    .ToList();

                var activeSemester = allSemesters.FirstOrDefault(s => s.IsActive);
                var cutoffDate = activeSemester?.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
                FutureSemesters = allSemesters
                    .Where(s => s.StartDate > cutoffDate)
                    .OrderBy(s => s.StartDate)
                    .Select(s => new SemesterOption { SemesterId = s.SemesterId, SemesterName = s.SemesterName, IsActive = s.IsActive })
                    .ToList();
            }

            var request = new GetCoursesRequest
            {
                Keyword = Keyword,
                IsActive = IsActive,
                SemesterId = SemesterId,
                Page = CurrentPage <= 0 ? 1 : CurrentPage,
                PageSize = PageSize <= 0 ? 20 : PageSize
            };

            var result = await _courseManagementService.GetCoursesAsync(userId, nameof(UserRole.ADMIN), request);

            if (result.IsSuccess && result.Data is not null)
            {
                CoursePage = result.Data;
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CourseManagement OnGetAsync error. UserId={UserId}", userId);
            ErrorMessage = "An unexpected error occurred while loading courses.";
        }

        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync()
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        try
        {
            var result = await _courseManagementService.CreateCourseAsync(userId, nameof(UserRole.ADMIN), CreateRequest);

            if (result.IsSuccess)
            {
                SuccessMessage = result.Message ?? "Course created successfully.";
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CourseManagement OnPostCreateAsync error. UserId={UserId}", userId);
            ErrorMessage = "An unexpected error occurred while creating the course.";
        }

        return RedirectToPage("./Index", new { CurrentPage, Keyword, IsActive, SemesterId, PageSize });
    }

    public async Task<IActionResult> OnPostUpdateAsync()
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        try
        {
            var result = await _courseManagementService.UpdateCourseAsync(userId, nameof(UserRole.ADMIN), UpdateRequest);

            if (result.IsSuccess)
            {
                SuccessMessage = result.Message ?? "Course updated successfully.";
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CourseManagement OnPostUpdateAsync error. UserId={UserId}, CourseId={CourseId}", userId, UpdateRequest?.CourseId);
            ErrorMessage = "An unexpected error occurred while updating the course.";
        }

        return RedirectToPage("./Index", new { CurrentPage, Keyword, IsActive, SemesterId, PageSize });
    }

    public async Task<IActionResult> OnPostDeactivateAsync()
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        try
        {
            var result = await _courseManagementService.DeactivateCourseAsync(userId, nameof(UserRole.ADMIN), DeactivateRequest);

            if (result.IsSuccess && result.Data is not null)
            {
                var data = result.Data;
                SuccessMessage = $"Course deactivated. Closed sections: {data.ClosedSectionCount}, Dropped enrollments: {data.DroppedEnrollmentCount}, Affected students: {data.AffectedStudentCount}, Affected teachers: {data.AffectedTeacherCount}.";
            }
            else
            {
                ErrorMessage = result.Message;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CourseManagement OnPostDeactivateAsync error. UserId={UserId}, CourseId={CourseId}", userId, DeactivateRequest?.CourseId);
            ErrorMessage = "An unexpected error occurred while deactivating the course.";
        }

        return RedirectToPage("./Index", new { CurrentPage, Keyword, IsActive, SemesterId, PageSize });
    }

    public async Task<IActionResult> OnGetDetailAsync(int courseId)
    {
        var userId = GetUserId();
        if (userId == 0) return new JsonResult(new { success = false, message = "Unauthorized" });

        try
        {
            var result = await _courseManagementService.GetCourseDetailAsync(userId, nameof(UserRole.ADMIN), courseId);

            if (result.IsSuccess && result.Data is not null)
            {
                return new JsonResult(new { success = true, data = result.Data });
            }

            return new JsonResult(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CourseManagement OnGetDetailAsync error. CourseId={CourseId}", courseId);
            return new JsonResult(new { success = false, message = "An unexpected error occurred." });
        }
    }

    public async Task<IActionResult> OnGetSectionsAsync(int courseId)
    {
        var userId = GetUserId();
        if (userId == 0) return new JsonResult(new { success = false, message = "Unauthorized" });

        try
        {
            var result = await _courseManagementService.GetCourseSectionsAsync(userId, nameof(UserRole.ADMIN), courseId);

            if (result.IsSuccess && result.Data is not null)
            {
                return new JsonResult(new { success = true, data = result.Data });
            }

            return new JsonResult(new { success = false, message = result.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OnGetSectionsAsync error. CourseId={CourseId}", courseId);
            return new JsonResult(new { success = false, message = "Failed to load sections." });
        }
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var id) ? id : 0;
    }

    public sealed class SemesterOption
    {
        public int SemesterId { get; set; }
        public string SemesterName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
