using StackExchange.Redis;
using Valuator.Infrastructure;

namespace Valuator;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorPages();
        builder.Services.AddSignalR();

        var redisConnectionString = builder.Configuration.GetValue<string>("Redis:ConnectionString") ?? "127.0.0.1:6379";
        var rabbitMqHost = builder.Configuration.GetValue<string>("RabbitMq:HostName") ?? "127.0.0.1";
        var rabbitMqExchange = builder.Configuration.GetValue<string>("RabbitMq:ExchangeName") ?? "valuator.processing.rank";
        var rabbitMqQueue = builder.Configuration.GetValue<string>("RabbitMq:QueueName") ?? "valuator.processing.rank";
        var rabbitMqEventsExchange = builder.Configuration.GetValue<string>("RabbitMq:EventsExchangeName") ?? "events";

        builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
            ConnectionMultiplexer.Connect(redisConnectionString));

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

        builder.Services.AddHostedService<RankCalculatedEventConsumer>();

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();
        app.MapRazorPages();
        app.MapHub<RankHub>("/rankHub");
        app.Run();
    }
}
