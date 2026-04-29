using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

var settings = new ServiceSettings(
    builder.Configuration["Redis:ConnectionString"] ?? throw new Exception("Redis connection missing"),
    builder.Configuration["RabbitMq:HostName"] ?? "localhost",
    builder.Configuration["RabbitMq:ExchangeName"] ?? "rank-exchange",
    builder.Configuration["RabbitMq:QueueName"] ?? "rank-queue"
);

builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(settings.RedisUrl));
builder.Services.AddHostedService(sp => new RankCalculatorService(
    sp.GetRequiredService<IConnectionMultiplexer>(),
    settings));

var app = builder.Build();
await app.RunAsync();

public record ServiceSettings(string RedisUrl, string MqHost, string Exchange, string Queue);

public class RankCalculatorService : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ServiceSettings _cfg;

    public RankCalculatorService(IConnectionMultiplexer redis, ServiceSettings settings)
    {
        _redis = redis;
        _cfg = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory { HostName = _cfg.MqHost };

        using var connection = await factory.CreateConnectionAsync(ct);
        using var channel = await connection.CreateChannelAsync(null, ct);

        await channel.ExchangeDeclareAsync(_cfg.Exchange, ExchangeType.Direct, cancellationToken: ct);
        await channel.QueueDeclareAsync(_cfg.Queue, true, false, false, cancellationToken: ct);
        await channel.QueueBindAsync(_cfg.Queue, _cfg.Exchange, string.Empty, cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var id = Encoding.UTF8.GetString(ea.Body.ToArray());
                var db = _redis.GetDatabase();

                var textData = await db.StringGetAsync($"TEXT-{id}");
                var text = textData.HasValue ? textData.ToString() : string.Empty;

                var rank = CalculateScore(text);
                await db.StringSetAsync($"RANK-{id}", Math.Round(rank, 4));

                await channel.BasicAckAsync(ea.DeliveryTag, false);
            }
            catch
            {
                await channel.BasicNackAsync(ea.DeliveryTag, false, true);
            }
        };

        await channel.BasicConsumeAsync(_cfg.Queue, false, consumer, ct);

        await Task.Delay(Timeout.Infinite, ct);
    }

    private static double CalculateScore(string input)
    {
        if (string.IsNullOrEmpty(input)) return 0;

        int symbolsCount = input.Count(c => !char.IsLetter(c));
        return (double)symbolsCount / input.Length;
    }
}
