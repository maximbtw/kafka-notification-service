namespace Api.Contracts;

internal interface INotificationService
{
    Task<bool> SendNotificationAsync(SendNotificationRequest request, CancellationToken ct);
}