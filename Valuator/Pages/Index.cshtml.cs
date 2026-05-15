using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using Valuator.Infrastructure;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IDatabase _db;
    private readonly RankTaskPublisher _publisher;
    private readonly EventsPublisher _eventsPublisher;
    private readonly UserStore _userStore;

    public IndexModel(
        ILogger<IndexModel> logger,
        IConnectionMultiplexer redis,
        RankTaskPublisher publisher,
        EventsPublisher eventsPublisher,
        UserStore userStore)
    {
        _logger = logger;
        _db = redis.GetDatabase();
        _publisher = publisher;
        _eventsPublisher = eventsPublisher;
        _userStore = userStore;
    }

    [BindProperty]
    public string LoginUsername { get; set; } = string.Empty;
    [BindProperty]
    public string LoginPassword { get; set; } = string.Empty;
    [BindProperty]
    public string RegisterUsername { get; set; } = string.Empty;
    [BindProperty]
    public string RegisterPassword { get; set; } = string.Empty;

    public string Handler { get; set; } = string.Empty;
    public string AuthMessage { get; set; } = string.Empty;

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Page();
        }

        string id = Guid.NewGuid().ToString();

        string textKey = "TEXT-" + id;
        await _db.StringSetAsync(textKey, text);

        string similarityKey = "SIMILARITY-" + id;
        const string allTextsKey = "ALL_TEXTS";

        bool added = await _db.SetAddAsync(allTextsKey, text);
        int similarity = added ? 0 : 1;

        await _db.StringSetAsync(similarityKey, similarity);

        if (User.Identity?.IsAuthenticated == true)
        {
            await _db.StringSetAsync($"AUTHOR-{id}", User.Identity.Name);
        }

        var similarityEvent = new SimilarityCalculatedEvent(id, similarity);
        var eventJson = JsonSerializer.Serialize(similarityEvent);
        await _eventsPublisher.PublishEventAsync("similarity", eventJson);

        await _publisher.PublishAsync(id);

        return Redirect($"summary?id={id}");
    }

    public async Task<IActionResult> OnPostLoginAsync()
    {
        Handler = "login";
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

    public async Task<IActionResult> OnPostRegisterAsync()
    {
        Handler = "register";
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

    public async Task<IActionResult> OnPostLogoutAsync()
    {
        Handler = "logout";
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }
}

public record SimilarityCalculatedEvent(string Id, int Similarity);
