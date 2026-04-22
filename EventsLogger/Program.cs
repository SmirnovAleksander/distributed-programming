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

await channel.ExchangeDeclareAsync($"{eventsExchange}.rank", ExchangeType.Fanout, cancellationToken: CancellationToken.None);
await channel.ExchangeDeclareAsync($"{eventsExchange}.similarity", ExchangeType.Fanout, cancellationToken: CancellationToken.None);

var rankQueueResult = await channel.QueueDeclareAsync(cancellationToken: CancellationToken.None);
var rankQueueName = rankQueueResult.QueueName;
await channel.QueueBindAsync(rankQueueName, $"{eventsExchange}.rank", "RankCalculated", cancellationToken: CancellationToken.None);

var similarityQueueResult = await channel.QueueDeclareAsync(cancellationToken: CancellationToken.None);
var similarityQueueName = similarityQueueResult.QueueName;
await channel.QueueBindAsync(similarityQueueName, $"{eventsExchange}.similarity", "SimilarityCalculated", cancellationToken: CancellationToken.None);

var rankConsumer = new AsyncEventingBasicConsumer(channel);
rankConsumer.ReceivedAsync += async (_, ea) =>
{
    var body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);
    var evt = JsonSerializer.Deserialize<RankCalculatedEvent>(message);
    Console.WriteLine($"[RankCalculated] Id: {evt?.Id}, Rank: {evt?.Rank}");
    await channel.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
};

await channel.BasicConsumeAsync(rankQueueName, false, rankConsumer, CancellationToken.None);
Console.WriteLine($"Listening for RankCalculated events on queue: {rankQueueName}");

var similarityConsumer = new AsyncEventingBasicConsumer(channel);
similarityConsumer.ReceivedAsync += async (_, ea) =>
{
    var body = ea.Body.ToArray();
    var message = Encoding.UTF8.GetString(body);
    var evt = JsonSerializer.Deserialize<SimilarityCalculatedEvent>(message);
    Console.WriteLine($"[SimilarityCalculated] Id: {evt?.Id}, Similarity: {evt?.Similarity}");
    await channel.BasicAckAsync(ea.DeliveryTag, false, CancellationToken.None);
};

await channel.BasicConsumeAsync(similarityQueueName, false, similarityConsumer, CancellationToken.None);
Console.WriteLine($"Listening for SimilarityCalculated events on queue: {similarityQueueName}");

Console.WriteLine("EventsLogger is running. Press Ctrl+C to exit.");
await Task.Delay(Timeout.Infinite, CancellationToken.None);

public record RankCalculatedEvent(string Id, double Rank);
public record SimilarityCalculatedEvent(string Id, int Similarity);
