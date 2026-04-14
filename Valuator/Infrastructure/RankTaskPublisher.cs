using System.Text;
using RabbitMQ.Client;

namespace Valuator.Infrastructure;

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
            HostName = _options.HostName
        };

        await using IConnection connection = await factory.CreateConnectionAsync();
        await using IChannel channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Direct //маршутизуются по routingKey
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
