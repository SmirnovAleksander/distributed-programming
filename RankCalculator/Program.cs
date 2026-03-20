using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

public class Program
{
    private const string QueueName = "valuator.processing.rank";
    private const string ExchangeName = "valuator.processing.rank";

    public static async Task Main(string[] args)
    {
        var rabbitHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";
        var redisConnectionString =
            Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING") ??
            Environment.GetEnvironmentVariable("Redis:ConnectionString") ??
            "localhost:6379";

        var factory = new ConnectionFactory
        {
            HostName = rabbitHost
        };

        await using var connection = await factory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        // Ensure topology exists: exchange -> queue -> consumer.
        await channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Direct,
            cancellationToken: CancellationToken.None);

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: CancellationToken.None);

        await channel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: "",
            cancellationToken: CancellationToken.None);

        // Competing consumers fairness: process 1 message at a time.
        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: CancellationToken.None);

        using var redis = await ConnectionMultiplexer.ConnectAsync(redisConnectionString);
        var db = redis.GetDatabase();

        Console.WriteLine($"RankCalculator started. RabbitMQ={rabbitHost}, Redis={redisConnectionString}");
        Console.WriteLine("Press Ctrl+C to exit.");

        // Для демонстрации "не завершено" фейковой задержкой.
        // const int delayMs = 3000;
        // Console.WriteLine($"Artificial delay enabled: {delayMs} ms per message");

        var shutdownTcs = new TaskCompletionSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            shutdownTcs.TrySetResult();
        };

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            var id = Encoding.UTF8.GetString(eventArgs.Body.ToArray()).Trim();
            try
            {
                var text = db.StringGet("TEXT-" + id);
                //if (delayMs > 0)
                // await Task.Delay(delayMs);

                var rank = CalculateRank(text.HasValue ? text.ToString() : string.Empty);
                db.StringSet("RANK-" + id, rank.ToString());
                Console.WriteLine($"Processed id={id}, rank={rank}");
            }
            catch (Exception ex)
            {
                // For lab purposes: log error and ack to avoid infinite retry loops.
                Console.WriteLine($"Failed to process message id={id}. Error: {ex.Message}");
            }
            finally
            {
                await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
            }
        };

        var consumerTag = await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer,
            cancellationToken: CancellationToken.None);

        await shutdownTcs.Task;

        await channel.BasicCancelAsync(consumerTag, cancellationToken: CancellationToken.None);
        await redis.CloseAsync();
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
}
