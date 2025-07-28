namespace NotificationService.Services;

internal class SendNotificationParameters
{
    public string SenderPassword { get; set; } = string.Empty;

    public string SenderEmail { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public string Subject { get; set; } = string.Empty;

    public bool IsBodyHtml { get; set; }
    
    public List<string> RecipientEmails { get; set; } = new();
}