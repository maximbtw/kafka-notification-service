using Confluent.Kafka;
using NotificationService.Contracts;
using Utils.Kafka;

namespace NotificationService.Consumers;

internal class NotificationConsumer : BackgroundService
{
    private readonly IConsumer<Ignore, NotificationMessage> _consumer;
    private readonly ILogger<NotificationConsumer> _logger;
    private readonly IConfiguration _configuration;
    private readonly INotificationSenderService _notificationSenderService;

    public NotificationConsumer(
        INotificationSenderService notificationSenderService, 
        IConfiguration configuration,
        ILogger<NotificationConsumer> logger)
    {
        _logger = logger;
        _configuration = configuration;
        _notificationSenderService = notificationSenderService;

        var bootstrapServers = configuration.GetSection("KafkaOptions:BootstrapServers").Get<List<string>>()!;
        
        var config = new ConsumerConfig
        {
            GroupId = "1",
            BootstrapServers = string.Join(",", bootstrapServers),
            AutoOffsetReset = AutoOffsetReset.Earliest,
        };

        _consumer = new ConsumerBuilder<Ignore, NotificationMessage>(config)
            .SetValueDeserializer(new KafkaProtobufDeserializer<NotificationMessage>())
            .SetErrorHandler((_, e) => _logger.LogError($"Consumer Builder:{e.Reason}"))
            .Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        string topic = _configuration["KafkaOptions:NotificationTopic"]!;
        
        _consumer.Subscribe(topic);

        _logger.LogInformation("Kafka Topic Subscribed");
        
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessKafkaMessage(stoppingToken);
        }

        _consumer.Close();
        
        _logger.LogInformation("Kafka Topic Unsubscribed");
    }

    private async Task ProcessKafkaMessage(CancellationToken stoppingToken)
    {
        try
        {
            ConsumeResult<Ignore, NotificationMessage>? consumeResult = _consumer.Consume(stoppingToken);
            if (consumeResult.Message?.Value == null)
            {
                _logger.LogWarning("Received empty message from Kafka.");
                
                return;
            }
            
            NotificationMessage message = consumeResult.Message.Value;

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
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Kafka message.");
        }
    }
}