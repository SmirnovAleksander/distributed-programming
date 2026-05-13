using Valuator.Infrastructure;

namespace Valuator;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorPages();

        var mainDbConnection = builder.Configuration.GetValue<string>("DB_MAIN") ?? "localhost:6000";
        var ruConnection = builder.Configuration.GetValue<string>("DB_RU") ?? "localhost:6001";
        var euConnection = builder.Configuration.GetValue<string>("DB_EU") ?? "localhost:6002";
        var asiaConnection = builder.Configuration.GetValue<string>("DB_ASIA") ?? "localhost:6003";
        var rabbitMqHost = builder.Configuration.GetValue<string>("RabbitMq:HostName") ?? "127.0.0.1";
        var rabbitMqExchange = builder.Configuration.GetValue<string>("RabbitMq:ExchangeName") ?? "valuator.processing.rank";
        var rabbitMqQueue = builder.Configuration.GetValue<string>("RabbitMq:QueueName") ?? "valuator.processing.rank";
        var rabbitMqEventsExchange = builder.Configuration.GetValue<string>("RabbitMq:EventsExchangeName") ?? "events";

        builder.Services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<ShardManager>>();
            return new ShardManager(
                mainDbConnection,
                new Dictionary<string, string>
                {
                    ["RU"] = ruConnection,
                    ["EU"] = euConnection,
                    ["ASIA"] = asiaConnection
                },
                logger);
        });

        builder.Services.AddSingleton(new RabbitMqOptions
        {
            HostName = rabbitMqHost,
            ExchangeName = rabbitMqExchange,
            QueueName = rabbitMqQueue
        });

        builder.Services.AddSingleton(new EventsOptions
        {
            HostName = rabbitMqHost,
            ExchangeName = rabbitMqEventsExchange
        });

        builder.Services.AddSingleton<RankTaskPublisher>();
        builder.Services.AddSingleton<EventsPublisher>();

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
