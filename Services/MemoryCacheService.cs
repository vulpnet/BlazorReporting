using Microsoft.Extensions.Caching.Memory;

namespace BlazorReporting.Services;

public sealed class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _cfg;

    public MemoryCacheService(IMemoryCache cache, IConfiguration cfg)
    {
        _cache = cache;
        _cfg = cfg;
    }

    public Task<T?> GetAsync<T>(string key)
    {
        _cache.TryGetValue(key, out T? value);
        return Task.FromResult(value);
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? sliding = null, TimeSpan? absolute = null)
    {
        var slidingMin = _cfg.GetValue("Caching:SlidingExpirationMinutes", 5);
        var absoluteMin = _cfg.GetValue("Caching:AbsoluteExpirationMinutes", 30);

        _cache.Set(key, value, new MemoryCacheEntryOptions
        {
            SlidingExpiration = sliding ?? TimeSpan.FromMinutes(slidingMin),
            AbsoluteExpirationRelativeToNow = absolute ?? TimeSpan.FromMinutes(absoluteMin)
        });
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }
}
