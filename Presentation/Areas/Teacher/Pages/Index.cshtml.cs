using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Teacher.Pages
{
    [Authorize(Roles = nameof(UserRole.TEACHER))]
    public class IndexModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
