using Api.Configuration;
using Confluent.Kafka;
using Microsoft.Extensions.Options;
using NotificationService.Contracts;
using Utils.Kafka;

namespace Api.Producers;

internal class NotificationProducer : IDisposable
{
    private readonly IProducer<Null, NotificationMessage> _producer;

    public NotificationProducer(ILogger<NotificationProducer> logger, IOptions<KafkaOptions> options)
    {
        var producerConfig = new ProducerConfig
        {
            BootstrapServers = string.Join(",", options.Value.BootstrapServers),
            RetryBackoffMs = 1000, // время ожидания между попытками, 1 секунда
            RetryBackoffMaxMs = 10000, // максимальное время ожидания, 10 секунд
            SocketTimeoutMs = 5500,
            MessageTimeoutMs = 10000,
            EnableIdempotence = true    // Избегание дублирования сообщений
        };

        _producer = new ProducerBuilder<Null, NotificationMessage>(producerConfig)
            .SetValueSerializer(new KafkaProtobufSerializer<NotificationMessage>())
            .SetErrorHandler((_, e) => logger.LogError($"Kafka error: {e.Reason}"))
            .Build();
    }

    public async Task<bool> ProduceAsync(string topic, NotificationMessage message)
    {
        try
        {
            var kafkaMessage = new Message<Null, NotificationMessage> { Value = message };

            await _producer.ProduceAsync(topic, kafkaMessage);

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(10)); // Завершить отправку всех сообщений
        _producer.Dispose();
    }
}