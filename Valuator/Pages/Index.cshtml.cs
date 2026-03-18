using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IDatabase _redis;

    public IndexModel(ILogger<IndexModel> logger, IConnectionMultiplexer redis)
    {
        _logger = logger;
        _redis = redis.GetDatabase();
    }

    public void OnGet()
    {
    }

    public IActionResult OnPost(string text)
    {
        _logger.LogDebug(text);

        text ??= string.Empty;
        string id = Guid.NewGuid().ToString();

        string textKey = "TEXT-" + id;
        _redis.StringSet(textKey, text);

        double rank = CalculateRank(text);
        string rankKey = "RANK-" + id;
        _redis.StringSet(rankKey, rank.ToString());

        double similarity = CalculateSimilarity(text);
        string similarityKey = "SIMILARITY-" + id;
        _redis.StringSet(similarityKey, similarity.ToString());

        return Redirect($"summary?id={id}");
    }

    private static double CalculateRank(string text)
    {
        if (string.IsNullOrEmpty(text))
            return 0;

        int nonAlphabetic = 0;
        foreach (char c in text)
        {
            if (!char.IsLetter(c))
                nonAlphabetic++;
        }
        return (double)nonAlphabetic / text.Length;
    }
    private double CalculateSimilarity(string text)
    {
        string hash = ComputeHash(text);
        const string setKey = "processed-text-hashes";
        bool exists = _redis.SetContains(setKey, hash);
        _redis.SetAdd(setKey, hash);
        return exists ? 1 : 0;
    }

    private static string ComputeHash(string text)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }
}
