using System.Net;
using System.Net.Mail;
using NotificationService.Configuration;
using NotificationService.Contracts;

namespace NotificationService.Services;

internal class NotificationSenderService : INotificationSenderService
{
    private readonly ServiceConfiguration _configuration;

    private int _notificationCounter;

    public NotificationSenderService(ServiceConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task SendNotification(SendNotificationParameters parameters)
    {
        Interlocked.Increment(ref _notificationCounter);
        if (_notificationCounter % 1000 == 0)
        {
            throw new Exception("Custom exception");
        }
        
        // Some work..
        await Task.Delay(1000);
        
        return;
        
        var mailMessage = new MailMessage
        {
            From = new MailAddress(parameters.SenderEmail),
            Subject = parameters.Subject,
            Body = parameters.Body,
            IsBodyHtml = parameters.IsBodyHtml
        };

        foreach (string senderEmail in parameters.RecipientEmails)
        {
            mailMessage.To.Add(senderEmail);
        }

        using var smtpClient = new SmtpClient(
            _configuration.EmailOptions.SmtpServer, 
            _configuration.EmailOptions.SmtpPort);

        smtpClient.Credentials = new NetworkCredential(parameters.SenderEmail, parameters.SenderPassword);
        smtpClient.EnableSsl = true;

        await smtpClient.SendMailAsync(mailMessage);
    }
}