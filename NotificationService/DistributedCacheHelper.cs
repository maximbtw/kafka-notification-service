using Microsoft.Extensions.Caching.Distributed;

namespace NotificationService;

public static class DistributedCacheHelper
{
    public static async Task<bool> GetAsync(IDistributedCache cache, Guid key, CancellationToken ct)
    {
        if (cache == null) throw new ArgumentNullException(nameof(cache));

        string? cachedData = await cache.GetStringAsync(key.ToString(), ct);

        return cachedData != null;
    }

    public static async Task SetAsync(
        IDistributedCache cache, 
        Guid key, 
        int absoluteExpirationRelativeInSeconds,
        CancellationToken ct)
    {
        if (cache == null)
        {
            throw new ArgumentNullException(nameof(cache));
        }

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(absoluteExpirationRelativeInSeconds)
        };

        await cache.SetStringAsync(key.ToString(), "1", options, ct);
    }
}