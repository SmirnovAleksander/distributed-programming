using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;

namespace Valuator.Pages;

public class SummaryModel : PageModel
{
    private readonly ILogger<SummaryModel> _logger;
    private readonly IDatabase _redis;

    public SummaryModel(ILogger<SummaryModel> logger, IConnectionMultiplexer redis)
    {
        _logger = logger;
        _redis = redis.GetDatabase();
    }

    public double Rank { get; set; }
    public double Similarity { get; set; }

    public void OnGet(string id)
    {
        _logger.LogDebug(id ?? string.Empty);

        if (!string.IsNullOrEmpty(id))
        {
            var rankValue = _redis.StringGet("RANK-" + id);
            var similarityValue = _redis.StringGet("SIMILARITY-" + id);

            if (rankValue.HasValue && double.TryParse(rankValue, out double rank))
                Rank = rank;

            if (similarityValue.HasValue && double.TryParse(similarityValue, out double similarity))
                Similarity = similarity;
        }
    }
}
