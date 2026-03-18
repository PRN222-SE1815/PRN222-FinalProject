using System.Security.Claims;
using BusinessLogic.Services.Interfaces;
using BusinessObject.Enum;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace Presentation.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
    private readonly IAuthService _authService;

    public LoginModel(IAuthService authService)
    {
        _authService = authService;
    }

    [BindProperty]
    public LoginViewModel Input { get; set; } = new();

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (User.IsInRole(UserRole.ADMIN.ToString()))
            {
                return RedirectToPage("/Index", new { area = "Admin" });
            }

            if (User.IsInRole(UserRole.TEACHER.ToString()))
            {
                return RedirectToPage("/Index", new { area = "Teacher" });
            }

            if (User.IsInRole(UserRole.STUDENT.ToString()))
            {
                return RedirectToPage("/Index", new { area = "Student" });
            }

            return RedirectToPage("/Account/AccessDenied");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        try
        {
            var authUser = await _authService.LoginAsync(Input.Username, Input.Password);
            if (authUser == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password.");
                return Page();
            }

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, authUser.UserId.ToString()),
                new(ClaimTypes.Name, authUser.Username),
                new(ClaimTypes.Role, authUser.Role)
            };

            if (authUser.StudentId.HasValue)
            {
                claims.Add(new Claim("StudentId", authUser.StudentId.Value.ToString()));
            }

            if (authUser.TeacherId.HasValue)
            {
                claims.Add(new Claim("TeacherId", authUser.TeacherId.Value.ToString()));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            if (string.Equals(authUser.Role, UserRole.ADMIN.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToPage("/Index", new { area = "Admin" });
            }

            if (string.Equals(authUser.Role, UserRole.TEACHER.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToPage("/Index", new { area = "Teacher" });
            }

            if (string.Equals(authUser.Role, UserRole.STUDENT.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToPage("/Index", new { area = "Student" });
            }

            return RedirectToPage("/Account/AccessDenied");
        }
        catch
        {
            ModelState.AddModelError(string.Empty, "Unable to sign in. Please try again.");
            return Page();
        }
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Account/Login");
    }
    public class LoginViewModel
    {
        [Required]
        [StringLength(100)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;
    }
}
