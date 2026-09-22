using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace KosManager.Application.Cache;

public static class CacheKeys
{
    public static string Dashboard(string periode) => $"dash:{periode}";
    public const string Rooms = "rooms:list";
    public const string Tenants = "tenants:list";
    public static string Bills(int? tenantId, string status) => $"bills:{tenantId?.ToString() ?? "all"}:{status}";
    public const string Queue = "queue:verify";

    public static string PrefixOf(string key) => key.Split(':')[0];
}

/// <summary>Cache-aside dengan registrasi key per prefix agar invalidasi
/// deterministik (tanpa scan MemoryCache yang tak ada API-nya).</summary>
public class CacheHelper(IMemoryCache cache, ILogger<CacheHelper> log)
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, byte> _keys = new();

    public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan ttl)
    {
        if (cache.TryGetValue(key, out T? hit))
        {
            log.LogDebug("cache hit {Key}", key);
            return hit!;
        }
        log.LogDebug("cache miss {Key}", key);
        var value = await factory();
        cache.Set(key, value, ttl);
        _keys[key] = 0;
        return value;
    }

    public void InvalidatePrefix(string prefix)
    {
        foreach (var key in _keys.Keys.Where(k => k == prefix || k.StartsWith(prefix + ":")).ToList())
        {
            cache.Remove(key);
            _keys.TryRemove(key, out _);
        }
    }
}
