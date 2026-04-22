using System.Text.Json;
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

    public IndexModel(
        ILogger<IndexModel> logger,
        IConnectionMultiplexer redis,
        RankTaskPublisher publisher,
        EventsPublisher eventsPublisher)
    {
        _logger = logger;
        _db = redis.GetDatabase();
        _publisher = publisher;
        _eventsPublisher = eventsPublisher;
    }

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

        var similarityEvent = new SimilarityCalculatedEvent(id, similarity);
        var eventJson = JsonSerializer.Serialize(similarityEvent);
        await _eventsPublisher.PublishEventAsync("similarity", eventJson);

        await _publisher.PublishAsync(id);

        return Redirect($"summary?id={id}");
    }
}

public record SimilarityCalculatedEvent(string Id, int Similarity);
