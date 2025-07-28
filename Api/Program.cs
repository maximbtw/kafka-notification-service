using System.ComponentModel.DataAnnotations;
using Api.Configuration;
using Api.Contracts;
using Api.Jobs;
using Api.Producers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Quartz;
using Utils;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Configuration.Sources.Clear();
builder.Configuration
    .AddJsonFile("appsettings.json")
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true);

builder.Host.ConfigureServices((context, services) =>
{
    services.Configure<MessageSenderJobOptions>(context.Configuration.GetSection(nameof(MessageSenderJobOptions)));
    services.Configure<KafkaOptions>(context.Configuration.GetSection(nameof(KafkaOptions)));
    
    services.AddQuartz();
    
    services.AddEndpointsApiExplorer();
    services.AddSwaggerGen();

    services.AddSingleton<INotificationService, Api.Services.NotificationService>();
    services.AddSingleton<NotificationProducer>();
});

WebApplication app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapPost("/send-message",
    async (INotificationService service, [FromBody] SendNotificationRequest request) =>
    {
        var validationContext = new ValidationContext(request);
        var validationResults = new List<ValidationResult>();

        if (!Validator.TryValidateObject(request, validationContext, validationResults, validateAllProperties: true))
        {
            return Results.BadRequest(validationResults);
        }
        
        bool result = await service.SendNotificationAsync(request, new CancellationToken());

        return result ? Results.Ok() : Results.Problem("Unexpected error");
    });

app.MapPost("/activate-message-sender",
    async (ISchedulerFactory schedulerFactory, IOptions<MessageSenderJobOptions> options) =>
    {
        IScheduler scheduler = await schedulerFactory.GetScheduler();
        
        JobKey jobKey = MessageSenderScheduler.JobKey;
        bool jobExists = await scheduler.CheckExists(jobKey);

        if (jobExists)
        {
            IReadOnlyCollection<ITrigger> triggers = await scheduler.GetTriggersOfJob(jobKey);
            bool isActive = triggers.Any(trigger => trigger.GetNextFireTimeUtc() != null);
            if (isActive)
            {
                return Results.Ok("Job is already active.");
            }
            
            await scheduler.ResumeJob(jobKey);
            return Results.Ok("Job was inactive and has been reactivated.");
        }

        await MessageSenderScheduler.AddJob(scheduler, options.Value.UpdateIntervalInSeconds).ConfigureAwait(false);
        
        return Results.Ok("Job has been added and activated.");
    });

app.MapPost("/deactivate-message-sender",
    async (ISchedulerFactory schedulerFactory) =>
    {
        IScheduler scheduler = await schedulerFactory.GetScheduler();

        JobKey jobKey = MessageSenderScheduler.JobKey;
        bool jobExists = await scheduler.CheckExists(jobKey);
        if (!jobExists)
        {
            return Results.BadRequest("Job does not exist.");
        }

        await scheduler.PauseJob(jobKey);
        return Results.Ok("Job has been deactivated.");
    });

app.Run();