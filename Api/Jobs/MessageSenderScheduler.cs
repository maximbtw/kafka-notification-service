using Quartz;

namespace Api.Jobs;

internal static class MessageSenderScheduler
{
    public static JobKey JobKey => new(nameof(MessageSenderJob));
    
    public static async Task AddJob(IScheduler scheduler, int intervalInSeconds)
    {
        IJobDetail job = JobBuilder.Create<MessageSenderJob>()
            .WithIdentity(nameof(MessageSenderJob))
            .Build();
        
        ITrigger trigger = TriggerBuilder.Create()
            .WithIdentity($"{nameof(MessageSenderJob)}-trigger")
            .StartNow()
            .WithSimpleSchedule(x => x
                .WithIntervalInSeconds(intervalInSeconds)
                .RepeatForever())
            .Build();

        await scheduler.ScheduleJob(job, trigger);
    }
}