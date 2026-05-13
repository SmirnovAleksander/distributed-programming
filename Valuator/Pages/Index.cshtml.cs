using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Valuator.Infrastructure;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly ShardManager _shardManager;
    private readonly RankTaskPublisher _publisher;
    private readonly EventsPublisher _eventsPublisher;

    public string[] Countries => ShardManager.Countries;

    public IndexModel(
        ILogger<IndexModel> logger,
        ShardManager shardManager,
        RankTaskPublisher publisher,
        EventsPublisher eventsPublisher)
    {
        _logger = logger;
        _shardManager = shardManager;
        _publisher = publisher;
        _eventsPublisher = eventsPublisher;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(string text, string country)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Page();
        }

        string id = Guid.NewGuid().ToString();
        string region = ShardManager.GetRegion(country);
        var db = _shardManager.GetRegionDb(region);

        await db.StringSetAsync($"TEXT-{id}", text);

        bool added = await db.SetAddAsync("ALL_TEXTS", text);
        int similarity = added ? 0 : 1;

        await db.StringSetAsync($"SIMILARITY-{id}", similarity);

        await _shardManager.SetShardAsync(id, region);

        _logger.LogInformation("LOOKUP: {Id},  {Region}", id, region);

        var similarityEvent = new SimilarityCalculatedEvent(id, similarity, region);
        var eventJson = JsonSerializer.Serialize(similarityEvent);
        await _eventsPublisher.PublishEventAsync("similarity", eventJson);

        await _publisher.PublishAsync(id);

        return Redirect($"summary?id={id}");
    }
}

public record SimilarityCalculatedEvent(string Id, int Similarity, string Region);
