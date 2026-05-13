using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using Valuator.Infrastructure;

namespace Valuator.Pages;

public class SummaryModel : PageModel
{
    private readonly ILogger<SummaryModel> _logger;
    private readonly ShardManager _shardManager;

    public SummaryModel(ILogger<SummaryModel> logger, ShardManager shardManager)
    {
        _logger = logger;
        _shardManager = shardManager;
    }

    public double Rank { get; set; }
    public double Similarity { get; set; }
    public bool RankReady { get; set; }

    public async Task OnGetAsync(string id)
    {
        _logger.LogDebug(id);

        if (string.IsNullOrWhiteSpace(id))
        {
            Rank = 0.0;
            Similarity = 0.0;
            RankReady = false;
            return;
        }

        var region = await _shardManager.GetShardAsync(id);
        if (region == null)
        {
            Rank = 0.0;
            Similarity = 0.0;
            RankReady = false;
            return;
        }

        var db = _shardManager.GetRegionDb(region);

        RedisValue rankRaw = await db.StringGetAsync($"RANK-{id}");
        RankReady = !rankRaw.IsNull;
        Rank = rankRaw.IsNull ? 0.0 : (double)rankRaw;

        RedisValue simRaw = await db.StringGetAsync($"SIMILARITY-{id}");
        Similarity = simRaw.IsNull ? 0.0 : (double)simRaw;
    }
}
