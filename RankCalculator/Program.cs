using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;
using System.Text;

var builder = Host.CreateApplicationBuilder(args);

var redisConnectionString =
    builder.Configuration.GetValue<string>("Redis:ConnectionString")
    ?? throw new InvalidOperationException("Missing Redis:ConnectionString");

var rabbitMqHost =
    builder.Configuration.GetValue<string>("RabbitMq:HostName")
    ?? throw new InvalidOperationException("Missing RabbitMq:HostName");

var rabbitMqExchange =
    builder.Configuration.GetValue<string>("RabbitMq:ExchangeName")
    ?? throw new InvalidOperationException("Missing RabbitMq:ExchangeName");

var rabbitMqQueue =
    builder.Configuration.GetValue<string>("RabbitMq:QueueName")
    ?? throw new InvalidOperationException("Missing RabbitMq:QueueName");

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnectionString));

builder.Services.AddHostedService(sp =>
    new Worker(
        sp.GetRequiredService<IConnectionMultiplexer>(),
        rabbitMqHost,
        rabbitMqExchange,
        rabbitMqQueue));

await builder.Build().RunAsync();

public class Worker : BackgroundService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly string _host;
    private readonly string _exchange;
    private readonly string _queue;

    public Worker(
        IConnectionMultiplexer redis,
        string host,
        string exchange,
        string queue)
    {
        _redis = redis;
        _host = host;
        _exchange = exchange;
        _queue = queue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ConnectionFactory factory = new ConnectionFactory
        {
            HostName = _host
        };

        await using IConnection connection = await factory.CreateConnectionAsync(stoppingToken);
        await using IChannel channel = await connection.CreateChannelAsync(null, stoppingToken);

        await channel.ExchangeDeclareAsync(
            exchange: _exchange,
            type: ExchangeType.Direct,
            cancellationToken: stoppingToken
        );

        await channel.QueueDeclareAsync(
            queue: _queue,
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken
        );

        await channel.QueueBindAsync(
            queue: _queue,
            exchange: _exchange,
            routingKey: "",
            cancellationToken: stoppingToken
        );

        await channel.BasicQosAsync(
            prefetchSize: 0,
            prefetchCount: 1,
            global: false,
            cancellationToken: stoppingToken
        );

        AsyncEventingBasicConsumer consumer = new(channel);
        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            string id = Encoding.UTF8.GetString(eventArgs.Body.ToArray());

            IDatabase db = _redis.GetDatabase();

            string textKey = "TEXT-" + id;
            string rankKey = "RANK-" + id;

            RedisValue textRaw = await db.StringGetAsync(textKey);
            string text = textRaw.IsNull ? "" : textRaw.ToString();

            double rank = CalcRank(text);
            rank = Math.Round(rank, 4);

            await db.StringSetAsync(rankKey, rank);

            await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
        };

        await channel.BasicConsumeAsync(
            queue: _queue,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken
        );

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private static double CalcRank(string text)
    {
        double noNormal = 0.0;
        double result = 0.0;

        if (text.Length == 0)
        {
            return 0.0;
        }

        foreach (char value in text)
        {
            if (!char.IsLetter(value))
            {
                noNormal++;
            }
        }

        result = noNormal / text.Length;

        return result;
    }
}
