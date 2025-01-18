using System.Net;
using System.Net.Mail;
using NotificationService.Contracts;

namespace NotificationService.Services;

internal class NotificationSenderService(IConfiguration configuration) : INotificationSenderService
{
    private readonly string _smtpServer = configuration["EmailOptions:SmtpServer"]!;
    private readonly int _smtpPort = int.Parse(configuration["EmailOptions:SmtpPort"]!);

    public async Task SendNotification(SendNotificationParameters parameters)
    {
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

        using var smtpClient = new SmtpClient(_smtpServer, _smtpPort);

        smtpClient.Credentials = new NetworkCredential(parameters.SenderEmail, parameters.SenderPassword);
        smtpClient.EnableSsl = true;

        await smtpClient.SendMailAsync(mailMessage);
    }
}