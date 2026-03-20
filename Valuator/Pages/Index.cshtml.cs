using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RabbitMQ.Client;
using StackExchange.Redis;

namespace Valuator.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IDatabase _redis;

    private const string RabbitMqExchangeName = "valuator.processing.rank";
    private const string RabbitMqQueueName = "valuator.processing.rank";

    public IndexModel(ILogger<IndexModel> logger, IConnectionMultiplexer redis)
    {
        _logger = logger;
        _redis = redis.GetDatabase();
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPost(string text)
    {
        _logger.LogDebug(text);

        if (string.IsNullOrWhiteSpace(text))
        {
            return Page();
        }

        string id = Guid.NewGuid().ToString();

        string textKey = "TEXT-" + id;
        _redis.StringSet(textKey, text);

        double similarity = CalculateSimilarity(text);
        string similarityKey = "SIMILARITY-" + id;
        _redis.StringSet(similarityKey, similarity.ToString());

        await PublishRankJobAsync(id);

        return Redirect($"summary?id={id}");
    }

    private static async Task PublishRankJobAsync(string id)
    {
        var factory = new ConnectionFactory
        {
            HostName = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost"
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        var body = Encoding.UTF8.GetBytes(id);

        // Ensure topology exists so publishing doesn't fail.
        await channel.ExchangeDeclareAsync(
            exchange: RabbitMqExchangeName,
            type: ExchangeType.Direct,
            cancellationToken: CancellationToken.None);

        await channel.QueueDeclareAsync(
            queue: RabbitMqQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: CancellationToken.None);

        await channel.QueueBindAsync(
            queue: RabbitMqQueueName,
            exchange: RabbitMqExchangeName,
            routingKey: "",
            cancellationToken: CancellationToken.None);

        await channel.BasicPublishAsync(
            exchange: RabbitMqExchangeName,
            routingKey: "",
            mandatory: false,
            body: body,
            cancellationToken: CancellationToken.None);
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
