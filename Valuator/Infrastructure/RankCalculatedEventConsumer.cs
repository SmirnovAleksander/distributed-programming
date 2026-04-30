using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Valuator.Hubs;

namespace Valuator.Infrastructure;

public record RankCalculatedEventDto(string Id, double Rank);

public class RankCalculatedEventConsumer : BackgroundService
{
    private readonly IHubContext<RankHub> _hubContext;
    private readonly EventsOptions _events;
    private readonly ILogger<RankCalculatedEventConsumer> _logger;

    public RankCalculatedEventConsumer(
        IHubContext<RankHub> hubContext,
        EventsOptions events,
        ILogger<RankCalculatedEventConsumer> logger)
    {
        _hubContext = hubContext;
        _events = events;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory { HostName = _events.HostName };

        await using var connection = await factory.CreateConnectionAsync(stoppingToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        var exchange = $"{_events.ExchangeName}.rank";
        await channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: ExchangeType.Fanout,
            cancellationToken: stoppingToken);

        var queue = await channel.QueueDeclareAsync(cancellationToken: stoppingToken);
        await channel.QueueBindAsync(
            queue: queue.QueueName,
            exchange: exchange,
            routingKey: string.Empty,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, ea) =>
        {
            try
            {
                var json = Encoding.UTF8.GetString(ea.Body.Span);
                var evt = JsonSerializer.Deserialize<RankCalculatedEventDto>(json);
                if (evt != null && !string.IsNullOrEmpty(evt.Id))
                {
                    await _hubContext.Clients.Group(RankHub.GroupFor(evt.Id)).SendAsync("RankReady", evt.Rank);
                }

                await channel.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process RankCalculated event");
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
            }
        };

        await channel.BasicConsumeAsync(queue.QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // shutting down
        }
    }
}
