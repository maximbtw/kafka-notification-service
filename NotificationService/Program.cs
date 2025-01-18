using NotificationService.Consumers;
using NotificationService.Contracts;
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
    services.AddSingleton<INotificationSenderService, NotificationSenderService>();

    services.AddHostedService<NotificationConsumer>();
});

IHost host = builder.Build();
host.Run();
