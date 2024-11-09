using System.Diagnostics.CodeAnalysis;

namespace BlueskyFeed.Api.Services;

public class ResponseCacheService : IService, IDisposable
{
    public ResponseCacheService(ILogger<ResponseCacheService> logger)
    {
        _logger = logger;
        _timer = new Timer(OnTimer, null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
    }
    
    private readonly Dictionary<string, (DateTime Expire, FeedResponse Response)> _feedCache = new();
    private readonly ILogger<ResponseCacheService> _logger;
    private readonly Timer _timer;
    
    public bool TryGet(string key, [MaybeNullWhen(false)] out FeedResponse response)
    {
        if (_feedCache.TryGetValue(key, out var value))
        {
            response = value.Response;
            return true;
        }
        response = null;
        return false;
    }
    
    private void OnTimer(object? state)
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _feedCache
            .Where(x => x.Value.Expire < now)
            .Select(x => x.Key)
            .ToArray();
        foreach (var key in expiredKeys)
        {
            _feedCache.Remove(key);
        }
    }
    
    public void Set(string key, FeedResponse response)
    {
        _feedCache[key] = (DateTime.UtcNow.AddMinutes(1), response);
    }
    
    public void Dispose()
    {
        _timer.Dispose();
    }
}