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
            var processor = new KafkaMessageProcessor(
                _consumer, 
                _logger, 
                _notificationSenderService,
                _distributedCache,
                _configuration);

            var commiter = new KafkaOffsetCommitter(_consumer, _logger);
            Task ackTask = commiter.StartRead(ct);

            Task produce = processor.Produce(commiter, ct);

            Task[] consumeTasks = Enumerable.Range(0, _configuration.MaxParallelism)
                .Select(_ => processor.Consume(commiter,  ct))
                .ToArray();

            await produce;
            await Task.WhenAll(consumeTasks);

            commiter.StopRead();

            await ackTask;
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
}