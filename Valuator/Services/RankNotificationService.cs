using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Valuator.Hubs;
using Valuator.Infrastructure;

namespace Valuator.Services;

public class RankNotificationService : BackgroundService
{
    private readonly EventsOptions _eventsOptions;
    private readonly IHubContext<RankUpdatesHub> _hubContext;
    private readonly ILogger<RankNotificationService> _logger;

    public RankNotificationService(
        EventsOptions eventsOptions,
        IHubContext<RankUpdatesHub> hubContext,
        ILogger<RankNotificationService> logger)
    {
        _eventsOptions = eventsOptions;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory { HostName = _eventsOptions.HostName };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        var exchangeName = $"{_eventsOptions.ExchangeName}.rank";
        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Fanout,
            cancellationToken: stoppingToken);

        var queue = await channel.QueueDeclareAsync(
            queue: string.Empty,
            durable: false,
            exclusive: true,
            autoDelete: true,
            cancellationToken: stoppingToken);

        await channel.QueueBindAsync(
            queue: queue.QueueName,
            exchange: exchangeName,
            routingKey: string.Empty,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var body = Encoding.UTF8.GetString(ea.Body.ToArray());
                var rankEvent = JsonSerializer.Deserialize<RankCalculatedEvent>(body);

                if (rankEvent is null || string.IsNullOrWhiteSpace(rankEvent.Id))
                {
                    return;
                }

                await _hubContext.Clients.Group(rankEvent.Id).SendAsync(
                    "rankUpdated",
                    rankEvent.Rank);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process rank event");
            }
        };

        await channel.BasicConsumeAsync(
            queue: queue.QueueName,
            autoAck: true,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }
}

public record RankCalculatedEvent(string Id, double Rank);
