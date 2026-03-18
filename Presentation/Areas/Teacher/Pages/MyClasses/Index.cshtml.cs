using System.Security.Claims;
using BusinessLogic.DTOs.Responses.Gradebook;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Teacher.Pages.MyClasses;

[Authorize(Roles = nameof(UserRole.TEACHER))]
public class IndexModel : PageModel
{
    private readonly IGradebookService _gradebookService;
    private readonly ILogger<IndexModel> _logger;

    public IndexModel(
        IGradebookService gradebookService,
        ILogger<IndexModel> logger)
    {
        _gradebookService = gradebookService;
        _logger = logger;
    }

    public IReadOnlyList<TeacherClassSectionDto> ClassSections { get; set; } = [];

    public List<SemesterOption> Semesters { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public int? SemesterId { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == 0) return RedirectToPage("/Account/Login");

        var result = await _gradebookService.GetTeacherClassSectionsAsync(
            userId,
            nameof(UserRole.TEACHER),
            ct);

        if (result.IsSuccess && result.Data is not null)
        {
            var allSections = result.Data;

            Semesters = allSections
                .Select(s => new SemesterOption { SemesterId = s.SemesterId, SemesterName = s.SemesterName })
                .DistinctBy(s => s.SemesterId)
                .OrderByDescending(s => s.SemesterName)
                .ToList();

            if (!SemesterId.HasValue && Semesters.Count > 0)
            {
                var activeResult = await _gradebookService.GetActiveSemesterIdAsync(ct);
                var activeSemesterId = activeResult.IsSuccess ? activeResult.Data : null;

                if (activeSemesterId.HasValue && Semesters.Any(s => s.SemesterId == activeSemesterId.Value))
                {
                    SemesterId = activeSemesterId.Value;
                }
                else
                {
                    SemesterId = Semesters.First().SemesterId;
                }
            }

            ClassSections = SemesterId.HasValue
                ? allSections.Where(s => s.SemesterId == SemesterId.Value).ToList()
                : allSections;
        }
        else
        {
            ErrorMessage = result.Message;
        }

        return Page();
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
    }
}
