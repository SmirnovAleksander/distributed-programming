using StackExchange.Redis;
using Valuator.Infrastructure;

namespace Valuator;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorPages();

        var redisConnectionString =
            builder.Configuration.GetValue<string>("Redis:ConnectionString")
            ?? throw new InvalidOperationException("Missing Redis:ConnectionString in appsettings.json");

        var rabbitMqHost =
            builder.Configuration.GetValue<string>("RabbitMq:HostName")
            ?? throw new InvalidOperationException("Missing RabbitMq:HostName in appsettings.json");

        var rabbitMqExchange =
            builder.Configuration.GetValue<string>("RabbitMq:ExchangeName")
            ?? throw new InvalidOperationException("Missing RabbitMq:ExchangeName in appsettings.json");

        var rabbitMqQueue =
            builder.Configuration.GetValue<string>("RabbitMq:QueueName")
            ?? throw new InvalidOperationException("Missing RabbitMq:QueueName in appsettings.json");

        var mux = ConnectionMultiplexer.Connect(redisConnectionString);
        builder.Services.AddSingleton<IConnectionMultiplexer>(mux);

        builder.Services.AddSingleton(new RabbitMqOptions
        {
            HostName = rabbitMqHost,
            ExchangeName = rabbitMqExchange,
            QueueName = rabbitMqQueue
        });

        builder.Services.AddSingleton<RankTaskPublisher>();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();
        app.MapRazorPages();
        app.Run();
    }
}
