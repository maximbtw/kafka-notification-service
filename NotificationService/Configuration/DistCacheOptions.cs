namespace NotificationService.Configuration;

internal class DistCacheOptions
{
    public string RedisUrl { get; set; } = string.Empty;
    
    public int AbsoluteExpirationRelativeInSeconds { get; set; }
}