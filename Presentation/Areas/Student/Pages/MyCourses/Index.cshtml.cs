using System.Security.Claims;
using BusinessLogic.DTOs.Response;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Student.Pages.MyCourses;

[Authorize(Roles = nameof(UserRole.STUDENT))]
public class IndexModel : PageModel
{
    private readonly IEnrollmentService _enrollmentService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(IEnrollmentService enrollmentService, ILogger<IndexModel> logger)
    {
        _enrollmentService = enrollmentService;
        _logger = logger;
    }

    public MyCoursesPageDto CoursesPage { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? SemesterId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int PageNum { get; set; } = 1;

    [TempData]
    public string? ErrorMessage { get; set; }

    public int TotalPages => CoursesPage.TotalCount > 0
        ? (int)Math.Ceiling((double)CoursesPage.TotalCount / CoursesPage.PageSize)
        : 1;

    public async Task OnGetAsync()
    {
        try
        {
            var userId = GetUserId();
            CoursesPage = await _enrollmentService.GetMyCoursesAsync(userId, SemesterId, PageNum);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MyCourses OnGetAsync error");
            ErrorMessage = "Có lỗi xảy ra, vui lòng thử lại.";
        }
    }

    private int GetUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier);
        return claim != null ? int.Parse(claim.Value) : 0;
    }
}
