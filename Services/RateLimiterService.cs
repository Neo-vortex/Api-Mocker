using System.Collections.Concurrent;

namespace ApiMocker.Services;

public class RateLimiterService
{
    private readonly ConcurrentDictionary<int, RateLimitBucket> _buckets = new();

    /// <summary>
    /// Returns true if the request is allowed, false if rate limit exceeded.
    /// </summary>
    public bool IsAllowed(int routeId, int maxRequests, int windowSeconds)
    {
        var bucket = _buckets.GetOrAdd(routeId, _ => new RateLimitBucket());
        return bucket.TryConsume(maxRequests, TimeSpan.FromSeconds(windowSeconds));
    }

    public void Reset(int routeId) => _buckets.TryRemove(routeId, out _);
}

public class RateLimitBucket
{
    private readonly object _lock = new();
    private readonly Queue<DateTime> _timestamps = new();

    public bool TryConsume(int maxRequests, TimeSpan window)
    {
        lock (_lock)
        {
            var now = DateTime.UtcNow;
            var cutoff = now - window;

            // Evict expired timestamps
            while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
                _timestamps.Dequeue();

            if (_timestamps.Count >= maxRequests)
                return false;

            _timestamps.Enqueue(now);
            return true;
        }
    }
}
