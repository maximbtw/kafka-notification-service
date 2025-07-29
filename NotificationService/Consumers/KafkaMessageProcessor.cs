using System.Collections.Concurrent;
using System.Threading.Channels;
using Confluent.Kafka;
using Microsoft.Extensions.Caching.Distributed;
using NotificationService.Configuration;
using NotificationService.Contracts;
using NotificationService.Services;

namespace NotificationService.Consumers;

internal class KafkaMessageProcessor
{
    private const int MaxRetry = 5;
    
    private readonly IConsumer<Ignore, NotificationMessage> _consumer;
    private readonly ILogger<NotificationConsumer> _logger;
    private readonly INotificationSenderService _notificationSenderService;
    private readonly IDistributedCache _distributedCache;
    private readonly ServiceConfiguration _configuration;
    
    private readonly Channel<ConsumeResult<Ignore, NotificationMessage>> _channel;
    private readonly ConcurrentDictionary<Guid, int> _retryIndex = new();

    public KafkaMessageProcessor(
        IConsumer<Ignore, NotificationMessage> consumer, 
        ILogger<NotificationConsumer> logger,
        INotificationSenderService notificationSenderService,
        IDistributedCache distributedCache,
        ServiceConfiguration configuration)
    {
        _consumer = consumer;
        _logger = logger;
        _notificationSenderService = notificationSenderService;
        _distributedCache = distributedCache;
        _configuration = configuration;

        _channel = Channel.CreateBounded<ConsumeResult<Ignore, NotificationMessage>>(
            new BoundedChannelOptions(configuration.MaxParallelism)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = true
            });
    }
    
    public async Task Produce(KafkaOffsetCommitter committer, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            ConsumeResult<Ignore, NotificationMessage>? consumeResult = _consumer.Consume(ct);

            if (consumeResult.Message?.Value == null)
            {
                _logger.LogWarning("Received empty message from Kafka.");

                continue;
            }

            NotificationMessage message = consumeResult.Message.Value;
            if (await DistributedCacheHelper.GetAsync(_distributedCache, message.Guid, ct))
            {
                _logger.LogInformation($"Message already processed: {message.Subject}");

                await committer.AddToCommit(consumeResult.TopicPartitionOffset, ct);

                continue;
            }

            await _channel.Writer.WriteAsync(consumeResult, ct);
        }

        _channel.Writer.Complete();
    }
    
    public async Task Consume(
        KafkaOffsetCommitter committer,
        CancellationToken ct)
    {
        await foreach (ConsumeResult<Ignore, NotificationMessage> consumeResult in _channel.Reader.ReadAllAsync(ct))
        {
            NotificationMessage message = consumeResult.Message.Value;
            try
            {

                var parameters = new SendNotificationParameters
                {
                    RecipientEmails = message.RecipientEmails,
                    SenderPassword = message.SenderPassword,
                    SenderEmail = message.SenderEmail,
                    Body = message.Body,
                    Subject = message.Subject,
                    IsBodyHtml = message.IsBodyHtml,
                };

                _logger.LogInformation($"Got message from Kafka: {message.Subject}");

                await _notificationSenderService.SendNotification(parameters);

                await DistributedCacheHelper.SetAsync(
                    _distributedCache,
                    message.Guid,
                    _configuration.DistCacheOptions.AbsoluteExpirationRelativeInSeconds,
                    ct);

                await committer.AddToCommit(consumeResult.TopicPartitionOffset, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing Kafka message: {message.Subject}");

                HandleFailedItem(consumeResult, ct);
            }
        }
    }

    private void HandleFailedItem(ConsumeResult<Ignore, NotificationMessage> consumeResult, CancellationToken ct)
    {
        NotificationMessage message = consumeResult.Message.Value;
        if (_retryIndex.TryGetValue(message.Guid, out int retry))
        {
            retry++;
            if (retry == MaxRetry)
            {
                _logger.LogCritical($"Max retry reached for message: {message.Subject}");

                _retryIndex.Remove(message.Guid, out _);
                
                return;
            }
            
            _retryIndex.AddOrUpdate(
                message.Guid,
                1,
                (_, prevRetry) => prevRetry + 1
            );
        }
        else
        {
            _retryIndex.TryAdd(message.Guid, 1);
        }
        
        TimeSpan delay = TimeSpan.FromSeconds(Math.Min(30, Math.Pow(2, retry)));
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(delay, ct);
                await _channel.Writer.WriteAsync(consumeResult, ct);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during retry delay and requeue.");
            }
        }, ct);
    }
}