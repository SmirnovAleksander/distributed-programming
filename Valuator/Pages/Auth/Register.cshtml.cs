using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Valuator.Infrastructure;

namespace Valuator.Pages.Auth;

public class RegisterModel : PageModel
{
    private readonly UserStore _userStore;

    public RegisterModel(UserStore userStore)
    {
        _userStore = userStore;
    }

    [BindProperty]
    public string RegisterUsername { get; set; } = string.Empty;

    [BindProperty]
    public string RegisterPassword { get; set; } = string.Empty;

    public string AuthMessage { get; set; } = string.Empty;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(RegisterUsername) || string.IsNullOrWhiteSpace(RegisterPassword))
        {
            AuthMessage = "Логин и пароль не могут быть пустыми";
            return Page();
        }

        var result = await _userStore.RegisterAsync(RegisterUsername, RegisterPassword);
        if (!result)
        {
            AuthMessage = "Пользователь с таким логином уже существует";
            return Page();
        }

        AuthMessage = "Регистрация успешна. Теперь вы можете войти.";
        return Page();
    }
}
