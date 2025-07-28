using Api.Configuration;
using Api.Contracts;
using Api.Producers;
using Microsoft.Extensions.Options;
using NotificationService.Contracts;

namespace Api.Services;

internal class NotificationService(NotificationProducer notificationProducer, IOptions<KafkaOptions> kafkaOptions)
    : INotificationService
{
    private readonly KafkaOptions _kafkaOptions = kafkaOptions.Value;

    public async Task<bool> SendNotificationAsync(SendNotificationRequest request, CancellationToken ct)
    {
        var message = new NotificationMessage
        {
            RecipientEmails = request.Emails,
            SenderEmail = _kafkaOptions.SenderEmail,
            SenderPassword = _kafkaOptions.SenderPassword,
            Subject = request.Subject,
            Body = request.Body,
            IsBodyHtml = request.IsBodyHtml,
            Guid = Guid.NewGuid()
        };

       return await notificationProducer.ProduceAsync(_kafkaOptions.NotificationTopic, message);
    }
}