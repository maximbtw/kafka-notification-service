using NotificationService.Configuration;
using NotificationService.Consumers;
using NotificationService.Services;

IHostBuilder builder = Host.CreateDefaultBuilder(args);

builder.ConfigureAppConfiguration((context, conf) =>
{
    conf.Sources.Clear();
    conf.AddJsonFile("appsettings.json", optional: false)
        .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
});

builder.ConfigureServices((context, services) =>
{
    var configuration = context.Configuration.GetSection(nameof(ServiceConfiguration)).Get<ServiceConfiguration>()!;

    services.AddSingleton(configuration);
    services.AddSingleton<INotificationSenderService, NotificationSenderService>();
    
    services.AddStackExchangeRedisCache(
        cacheOptions =>
        {
            cacheOptions.Configuration = configuration.DistCacheOptions.RedisUrl;
            cacheOptions.InstanceName = "dist-cache";
        }
    );
    services.AddHostedService<NotificationConsumer>();
});

IHost host = builder.Build();
host.Run();
