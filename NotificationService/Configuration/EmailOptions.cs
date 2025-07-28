namespace NotificationService.Configuration;

internal class EmailOptions
{
    public string SmtpServer { get; set; } = string.Empty;
    
    public int SmtpPort { get; set; }
}