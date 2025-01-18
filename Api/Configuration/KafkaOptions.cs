namespace Api.Configuration;

public class KafkaOptions
{
    public List<string> BootstrapServers { get; set; } = new();
    
    public string SenderEmail { get; set; } = string.Empty;

    public string SenderPassword { get; set; } = null!;
    
    public string NotificationTopic { get; set; } = string.Empty;
}