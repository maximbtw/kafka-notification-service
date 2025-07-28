using System.ComponentModel.DataAnnotations;

namespace NotificationService.Configuration;

internal class ServiceConfiguration
{
    public int MaxParallelism { get; set; }
    
    [Required]
    public EmailOptions EmailOptions { get; set; } = new();
    
    [Required]
    public DistCacheOptions DistCacheOptions { get; set; } = new();
    
    [Required]
    public KafkaOptions KafkaOptions { get; set; } = new();
}