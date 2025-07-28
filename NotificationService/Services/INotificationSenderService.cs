using NotificationService.Contracts;

namespace NotificationService.Services;

internal interface INotificationSenderService
{
    Task SendNotification(SendNotificationParameters parameters);
}