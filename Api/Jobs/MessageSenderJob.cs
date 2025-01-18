using Api.Configuration;
using Api.Contracts;
using Microsoft.Extensions.Options;
using Quartz;

namespace Api.Jobs;

[DisallowConcurrentExecution]
internal class MessageSenderJob(
    INotificationService notificationService,
    IOptions<MessageSenderJobOptions> options,
    ILogger<MessageSenderJob> logger)
    : IJob
{
    private readonly MessageSenderJobOptions _options = options.Value;
    private static int _counter = 1;

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            var request = new SendNotificationRequest
            {
                Emails = _options.Emails,
                Subject = $"Test notification #{_counter}",
                Body = _options.Body,
                IsBodyHtml = false
            };

            await notificationService.SendNotificationAsync(request);

            _counter++;

        }
        catch (Exception e)
        {
            logger.LogError($"Error while sending message: {e.Message}");
        }
    }
}