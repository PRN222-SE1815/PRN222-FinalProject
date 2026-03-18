using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Student.Pages
{
    [Authorize(Roles = nameof(UserRole.STUDENT))]
    public class IndexModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
