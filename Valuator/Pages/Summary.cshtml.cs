using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;

namespace Valuator.Pages;

public class SummaryModel : PageModel
{
    private readonly ILogger<SummaryModel> _logger;
    private readonly IDatabase _db;

    public SummaryModel(ILogger<SummaryModel> logger, IConnectionMultiplexer redis)
    {
        _logger = logger;
        _db = redis.GetDatabase();
    }

    public double Rank { get; set; }
    public double Similarity { get; set; }
    public bool RankReady { get; set; }

    public void OnGet(string id)
    {
        _logger.LogDebug(id);

        if (string.IsNullOrWhiteSpace(id))
        {
            Rank = 0.0;
            Similarity = 0.0;
            RankReady = false;
            return;
        }

        string rankKey = "RANK-" + id;
        string similarityKey = "SIMILARITY-" + id;

        RedisValue rankRaw = _db.StringGet(rankKey);
        RankReady = !rankRaw.IsNull;
        Rank = rankRaw.IsNull ? 0.0 : (double)rankRaw;

        RedisValue simRaw = _db.StringGet(similarityKey);
        Similarity = simRaw.IsNull ? 0.0 : (double)simRaw;
    }
}
