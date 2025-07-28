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
        var options = new ParallelOptions { MaxDegreeOfParallelism = _options.MaxParallelism };
        await Parallel.ForAsync(0, _options.MaxParallelism, options, async (_, ct) =>
        {
            for (int j = 0; j < _options.NotificationPerThread; j++)
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

                    await notificationService.SendNotificationAsync(request, ct);

                    Interlocked.Increment(ref _counter);
                }
                catch (Exception e)
                {
                    logger.LogError($"Error while sending message: {e.Message}");
                }
            }
        });
    }
}