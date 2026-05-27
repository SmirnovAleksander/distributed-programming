using System.Text;
using RabbitMQ.Client;

namespace Valuator.Infrastructure;

public class RabbitMqOptions
{
    public string HostName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ExchangeName { get; set; } = string.Empty;
    public string QueueName { get; set; } = string.Empty;
}

public class EventsOptions
{
    public string HostName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ExchangeName { get; set; } = string.Empty;
}

public class RankTaskPublisher
{
    private readonly RabbitMqOptions _options;

    public RankTaskPublisher(RabbitMqOptions options)
    {
        _options = options;
    }

    public async Task PublishAsync(string id)
    {
        ConnectionFactory factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            UserName = _options.UserName,
            Password = _options.Password
        };

        await using IConnection connection = await factory.CreateConnectionAsync();
        await using IChannel channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Direct
        );

        await channel.QueueDeclareAsync(
            queue: _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false
        );

        await channel.QueueBindAsync(
            queue: _options.QueueName,
            exchange: _options.ExchangeName,
            routingKey: ""
        );

        byte[] message = Encoding.UTF8.GetBytes(id);

        await channel.BasicPublishAsync(
            exchange: _options.ExchangeName,
            routingKey: "",
            mandatory: false,
            body: message
        );
    }
}

public class EventsPublisher
{
    private readonly EventsOptions _options;

    public EventsPublisher(EventsOptions options)
    {
        _options = options;
    }

    public async Task PublishEventAsync(string eventType, string message)
    {
        ConnectionFactory factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            UserName = _options.UserName,
            Password = _options.Password
        };

        await using IConnection connection = await factory.CreateConnectionAsync();
        await using IChannel channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: $"{_options.ExchangeName}.{eventType}",
            type: ExchangeType.Fanout
        );

        byte[] body = Encoding.UTF8.GetBytes(message);

        await channel.BasicPublishAsync(
            exchange: $"{_options.ExchangeName}.{eventType}",
            routingKey: "",
            mandatory: false,
            body: body
        );
    }
}
