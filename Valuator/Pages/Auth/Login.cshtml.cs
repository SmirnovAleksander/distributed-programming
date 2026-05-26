using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Valuator.Infrastructure;

namespace Valuator.Pages.Auth;

public class LoginModel : PageModel
{
    private readonly UserStore _userStore;

    public LoginModel(UserStore userStore)
    {
        _userStore = userStore;
    }

    [BindProperty]
    public string LoginUsername { get; set; } = string.Empty;

    [BindProperty]
    public string LoginPassword { get; set; } = string.Empty;

    public string AuthMessage { get; set; } = string.Empty;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = await _userStore.ValidateAsync(LoginUsername, LoginPassword);
        if (userId == null)
        {
            AuthMessage = "Неверный логин или пароль";
            return Page();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, LoginUsername),
            new(ClaimTypes.NameIdentifier, userId)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

        return Redirect("/");
    }

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }
}
