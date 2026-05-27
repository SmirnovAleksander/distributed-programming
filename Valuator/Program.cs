using Microsoft.AspNetCore.Authentication.Cookies;
using StackExchange.Redis;
using Valuator.Infrastructure;

namespace Valuator;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddRazorPages();

        var redisConnectionString = builder.Configuration.GetValue<string>("Redis:ConnectionString") ?? "127.0.0.1:6379";
        var redisPassword = builder.Configuration.GetValue<string>("Redis:Password") ?? "";
        var rabbitMqHost = builder.Configuration.GetValue<string>("RabbitMq:HostName") ?? "127.0.0.1";
        var rabbitMqUserName = builder.Configuration.GetValue<string>("RabbitMq:UserName") ?? "";
        var rabbitMqPassword = builder.Configuration.GetValue<string>("RabbitMq:Password") ?? "";
        var rabbitMqExchange = builder.Configuration.GetValue<string>("RabbitMq:ExchangeName") ?? "valuator.processing.rank";
        var rabbitMqQueue = builder.Configuration.GetValue<string>("RabbitMq:QueueName") ?? "valuator.processing.rank";
        var rabbitMqEventsExchange = builder.Configuration.GetValue<string>("RabbitMq:EventsExchangeName") ?? "events";

        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(new ConfigurationOptions
            {
                EndPoints = { redisConnectionString },
                Password = redisPassword
            }));

        builder.Services.AddSingleton(new RabbitMqOptions
        {
            HostName = rabbitMqHost,
            UserName = rabbitMqUserName,
            Password = rabbitMqPassword,
            ExchangeName = rabbitMqExchange,
            QueueName = rabbitMqQueue
        });

        builder.Services.AddSingleton(new EventsOptions
        {
            HostName = rabbitMqHost,
            UserName = rabbitMqUserName,
            Password = rabbitMqPassword,
            ExchangeName = rabbitMqEventsExchange
        });

        builder.Services.AddSingleton<RankTaskPublisher>();
        builder.Services.AddSingleton<EventsPublisher>();
        builder.Services.AddSingleton<UserStore>();

        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/";
                options.LogoutPath = "/";
                options.AccessDeniedPath = "/";
            });

        var app = builder.Build();

        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapRazorPages();
        app.Run();
    }
}
