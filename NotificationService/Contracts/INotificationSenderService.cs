namespace NotificationService.Contracts;

internal interface INotificationSenderService
{
    Task SendNotification(SendNotificationParameters parameters);
}