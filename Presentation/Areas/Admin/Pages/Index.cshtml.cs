using BusinessObject.Enum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Presentation.Areas.Admin.Pages
{
    [Authorize(Roles = nameof(UserRole.ADMIN))]
    public class IndexModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
