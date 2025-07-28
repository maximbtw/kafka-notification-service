using System.Threading.Channels;
using Confluent.Kafka;
using Microsoft.Extensions.Caching.Distributed;
using NotificationService.Configuration;
using NotificationService.Contracts;
using NotificationService.Services;
using Utils.Kafka;

namespace NotificationService.Consumers;

internal class NotificationConsumer : BackgroundService
{
    private readonly IConsumer<Ignore, NotificationMessage> _consumer;
    private readonly ILogger<NotificationConsumer> _logger;
    private readonly ServiceConfiguration _configuration;
    private readonly INotificationSenderService _notificationSenderService;
    private readonly IDistributedCache _distributedCache;

    public NotificationConsumer(
        INotificationSenderService notificationSenderService,
        ServiceConfiguration configuration,
        ILogger<NotificationConsumer> logger,
        IDistributedCache cache)
    {
        _distributedCache = cache;
        _logger = logger;
        _configuration = configuration;
        _notificationSenderService = notificationSenderService;

        var config = new ConsumerConfig
        {
            GroupId = "1",
            BootstrapServers = string.Join(",", configuration.KafkaOptions.BootstrapServers),
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<Ignore, NotificationMessage>(config)
            .SetValueDeserializer(new KafkaProtobufDeserializer<NotificationMessage>())
            .SetErrorHandler((_, e) => _logger.LogError($"Consumer Builder:{e.Reason}"))
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _consumer.Subscribe(_configuration.KafkaOptions.Topic);

        _logger.LogInformation("Kafka Topic Subscribed");

        try
        {
            var processChannel = Channel.CreateBounded<ConsumeResult<Ignore, NotificationMessage>>(
                new BoundedChannelOptions(_configuration.MaxParallelism)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleReader = false,
                    SingleWriter = true
                });

            var ackChannel = Channel.CreateUnbounded<ConsumeResult<Ignore, NotificationMessage>>(
                new UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });

            Task processProduce = ProcessProduce(processChannel.Writer, ackChannel.Writer, ct);
            Task ackConsume = AckConsume(ackChannel.Reader, ct);

            Task[] processConsumeTasks = Enumerable.Range(0, _configuration.MaxParallelism)
                .Select(_ => ProcessConsume(processChannel.Reader, ackChannel.Writer, ct))
                .ToArray();

            await Task.WhenAll(processConsumeTasks);

            ackChannel.Writer.Complete();

            await Task.WhenAll(processProduce, ackConsume);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            _consumer.Close();
            _logger.LogInformation("Kafka Topic Unsubscribed");
        }
    }

    private async Task ProcessProduce(
        ChannelWriter<ConsumeResult<Ignore, NotificationMessage>> writer,
        ChannelWriter<ConsumeResult<Ignore, NotificationMessage>> ackWriter,
        CancellationToken ct)
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

                await ackWriter.WriteAsync(consumeResult, ct);

                continue;
            }

            await writer.WriteAsync(consumeResult, ct);
        }

        writer.Complete();
    }

    private async Task ProcessConsume(
        ChannelReader<ConsumeResult<Ignore, NotificationMessage>> processReader,
        ChannelWriter<ConsumeResult<Ignore, NotificationMessage>> ackWriter,
        CancellationToken ct)
    {
        await foreach (ConsumeResult<Ignore, NotificationMessage> consumeResult in processReader.ReadAllAsync(ct))
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

                await ackWriter.WriteAsync(consumeResult, ct);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing Kafka message: {message.Subject}");
            }
        }
    }

    private async Task AckConsume(
        ChannelReader<ConsumeResult<Ignore, NotificationMessage>> ackReader,
        CancellationToken ct)
    {
        await foreach (ConsumeResult<Ignore, NotificationMessage> commitItem in ackReader.ReadAllAsync(ct))
        {
            NotificationMessage? message = commitItem.Message.Value;
            try
            {
                _consumer.Commit(commitItem);
                
                _logger.LogInformation($"Commiting message: {message.Subject}");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to commit Kafka message: {message.Subject}.");
            }
        }
    }
}