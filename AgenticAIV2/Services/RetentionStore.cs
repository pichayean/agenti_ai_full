using System.Collections.Concurrent;

namespace AgenticAI.Services;

public class RetentionStore
{
    private readonly TimeSpan _ttl;
    private readonly ConcurrentDictionary<string, (DateTimeOffset expire, object value)> _map = new();

    public RetentionStore(TimeSpan ttl) => _ttl = ttl;

    public string Put(object value)
    {
        var id = Guid.NewGuid().ToString("n");
        _map[id] = (DateTimeOffset.UtcNow.Add(_ttl), value);
        return id;
    }

    public T? Get<T>(string id)
    {
        if (_map.TryGetValue(id, out var entry))
        {
            if (entry.expire > DateTimeOffset.UtcNow) return (T)entry.value;
            _map.TryRemove(id, out _);
        }
        return default;
    }

    public void Sweep()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var kv in _map.ToArray())
            if (kv.Value.expire <= now) _map.TryRemove(kv.Key, out _);
    }
}
