namespace BlazorReporting.Services;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? sliding = null, TimeSpan? absolute = null);
    Task RemoveAsync(string key);

    /// <summary>Builds a stable cache key by joining parts with '|'.</summary>
    static string BuildKey(params string?[] parts) =>
        string.Join("|", parts.Select(p => p ?? "∅"));
}
