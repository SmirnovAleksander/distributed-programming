using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

Console.WriteLine("EventsLogger starting...");

var hostName = args.Length > 0 ? args[0] : "localhost";
var eventsExchange = args.Length > 1 ? args[1] : "events";

var factory = new ConnectionFactory { HostName = hostName };
await using var connection = await factory.CreateConnectionAsync();
await using var channel = await connection.CreateChannelAsync();

await channel.ExchangeDeclareAsync(
    exchange: $"{eventsExchange}.rank",
    type: ExchangeType.Fanout);

await channel.ExchangeDeclareAsync(
    exchange: $"{eventsExchange}.similarity",
    type: ExchangeType.Fanout);

//rank
var rankQueueResult = await channel.QueueDeclareAsync();
var rankQueueName = rankQueueResult.QueueName;
await channel.QueueBindAsync(
    queue: rankQueueName,
    exchange: $"{eventsExchange}.rank",
    routingKey: "RankCalculated");

//similarity
var similarityQueueResult = await channel.QueueDeclareAsync();
var similarityQueueName = similarityQueueResult.QueueName;
await channel.QueueBindAsync(
    queue: similarityQueueName,
    exchange: $"{eventsExchange}.similarity",
    routingKey: "SimilarityCalculated");

//rank
var rankConsumer = new AsyncEventingBasicConsumer(channel);
rankConsumer.ReceivedAsync += async (_, ea) =>
{
    try
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        var evt = JsonSerializer.Deserialize<RankCalculatedEvent>(message);
        Console.WriteLine($"[RankCalculated] Id: {evt?.Id}, Rank: {evt?.Rank}");
        if (evt != null)
            Console.WriteLine($"LOOKUP: {evt.Id},  {evt.Region}");
        await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
    }
    catch
    {
        await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
    }
};

await channel.BasicConsumeAsync(
    queue: rankQueueName,
    autoAck: false,
    consumer: rankConsumer);
Console.WriteLine($"Listening for RankCalculated events on queue: {rankQueueName}");

//similarity
var similarityConsumer = new AsyncEventingBasicConsumer(channel);
similarityConsumer.ReceivedAsync += async (_, ea) =>
{
    try
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        var evt = JsonSerializer.Deserialize<SimilarityCalculatedEvent>(message);
        Console.WriteLine($"[SimilarityCalculated] Id: {evt?.Id}, Similarity: {evt?.Similarity}");
        if (evt != null)
            Console.WriteLine($"LOOKUP: {evt.Id},  {evt.Region}");
        await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false);
    }
    catch
    {
        await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: true);
    }
};

await channel.BasicConsumeAsync(
    queue: similarityQueueName,
    autoAck: false,
    consumer: similarityConsumer);
Console.WriteLine($"Listening for SimilarityCalculated events on queue: {similarityQueueName}");

Console.WriteLine("EventsLogger is running. Press Ctrl+C to exit.");
await Task.Delay(Timeout.Infinite);

public record RankCalculatedEvent(string Id, double Rank, string Region);
public record SimilarityCalculatedEvent(string Id, int Similarity, string Region);
