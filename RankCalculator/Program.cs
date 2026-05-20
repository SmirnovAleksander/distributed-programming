using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared;

var builder = Host.CreateApplicationBuilder(args);

var mainDbConnection = builder.Configuration["DB_MAIN"] ?? "localhost:6000";
var ruConnection = builder.Configuration["DB_RU"] ?? "localhost:6001";
var euConnection = builder.Configuration["DB_EU"] ?? "localhost:6002";
var asiaConnection = builder.Configuration["DB_ASIA"] ?? "localhost:6003";

var settings = new ServiceSettings(
    mainDbConnection,
    new Dictionary<string, string>
    {
        ["RU"] = ruConnection,
        ["EU"] = euConnection,
        ["ASIA"] = asiaConnection
    },
    builder.Configuration["RabbitMq:HostName"] ?? "localhost",
    builder.Configuration["RabbitMq:ExchangeName"] ?? "rank-exchange",
    builder.Configuration["RabbitMq:QueueName"] ?? "rank-queue",
    builder.Configuration["RabbitMq:EventsExchangeName"] ?? "events"
);

builder.Services.AddHostedService(sp => new RankCalculatorService(settings));

var app = builder.Build();
await app.RunAsync();

public class RankCalculatorService : BackgroundService
{
    private readonly ServiceSettings _cfg;
    private readonly RedisShardManager _shardManager;

    public RankCalculatorService(ServiceSettings settings)
    {
        _cfg = settings;
        _shardManager = new RedisShardManager(settings.MainDbConnection, settings.RegionConnections);
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var factory = new ConnectionFactory { HostName = _cfg.MqHost };

        using var connection = await factory.CreateConnectionAsync(ct);
        var channel = await connection.CreateChannelAsync(null, ct);

        await channel.ExchangeDeclareAsync(
            exchange: _cfg.Exchange,
            type: ExchangeType.Direct,
            cancellationToken: ct);

        await channel.QueueDeclareAsync(
            queue: _cfg.Queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: ct);

        await channel.QueueBindAsync(
            queue: _cfg.Queue,
            exchange: _cfg.Exchange,
            routingKey: string.Empty,
            cancellationToken: ct);

        await channel.ExchangeDeclareAsync(
            exchange: $"{_cfg.EventsExchange}.rank",
            type: ExchangeType.Fanout,
            cancellationToken: ct);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var id = Encoding.UTF8.GetString(ea.Body.ToArray());

                var region = await _shardManager.GetShardAsync(id);
                if (region == null)
                {
                    await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
                    return;
                }
                Console.WriteLine($"LOOKUP: {id},  {region}");

                var db = _shardManager.GetRegionDb(region);

                var textData = await db.StringGetAsync($"TEXT-{id}");
                var text = textData.HasValue ? textData.ToString() : string.Empty;

                var rank = CalculateScore(text);
                rank = Math.Round(rank, 4);
                await db.StringSetAsync($"RANK-{id}", rank);

                var eventMessage = new RankCalculatedEvent(id, rank, region);
                var eventJson = JsonSerializer.Serialize(eventMessage);
                var eventBody = Encoding.UTF8.GetBytes(eventJson);

                await channel.BasicPublishAsync(
                    exchange: $"{_cfg.EventsExchange}.rank",
                    routingKey: "RankCalculated",
                    mandatory: false,
                    body: eventBody);

                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
            }
            catch
            {
                await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        await channel.BasicConsumeAsync(
            queue: _cfg.Queue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: ct);

        await Task.Delay(Timeout.Infinite, ct);
    }

    private static double CalculateScore(string input)
    {
        if (string.IsNullOrEmpty(input)) return 0;

        int symbolsCount = input.Count(c => !char.IsLetter(c));
        return (double)symbolsCount / input.Length;
    }
}

public record RankCalculatedEvent(string Id, double Rank, string Region);
