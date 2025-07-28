namespace NotificationService.Configuration;

internal class KafkaOptions
{
    public List<string> BootstrapServers { get; set; } = new();
    
    public string Topic { get; set; } = string.Empty;
}