namespace SabaMemDb.Engine;

public partial class StorageEngine : IDisposable
{
    public bool Expire(ReadOnlySpan<byte> key, int seconds)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);
        
        lock (_lockObj)
        {
            ref var entry = ref _index[bucket];
            
            if (entry.KeyLength == 0 || entry.KeyHash != hash || entry.KeyLength != key.Length) return false;
            ReadOnlySpan<byte> storedKey = _dataBuffer.AsSpan(entry.KeyOffset, entry.KeyLength);
            if (!key.SequenceEqual(storedKey)) return false;

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (entry.ExpiresAt > 0 && entry.ExpiresAt <= now)
            {
                entry = default;
                _count--;
                return false;
            }

            if (seconds <= 0)
            {
                entry = default;
                _count--;
                return true;
            }
            
            entry.ExpiresAt = now + (long)seconds * 1000L;
            return true;
        }
    }
    
    public bool PExpire(ReadOnlySpan<byte> key, int milliseconds)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);
        
        lock (_lockObj)
        {
            ref var entry = ref _index[bucket];
            
            if (entry.KeyLength == 0 || entry.KeyHash != hash || entry.KeyLength != key.Length) return false;
            ReadOnlySpan<byte> storedKey = _dataBuffer.AsSpan(entry.KeyOffset, entry.KeyLength);
            if (!key.SequenceEqual(storedKey)) return false;

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (entry.ExpiresAt > 0 && entry.ExpiresAt <= now)
            {
                entry = default;
                _count--;
                return false;
            }

            if (milliseconds <= 0)
            {
                entry = default;
                _count--;
                return true;
            }
            
            entry.ExpiresAt = now + (long)milliseconds;
            return true;
        }
    }
    
    public bool ExpireAt(ReadOnlySpan<byte> key, long timestamp)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);
        
        lock (_lockObj)
        {
            ref var entry = ref _index[bucket];
            
            if (entry.KeyLength == 0 || entry.KeyHash != hash || entry.KeyLength != key.Length) return false;
            ReadOnlySpan<byte> storedKey = _dataBuffer.AsSpan(entry.KeyOffset, entry.KeyLength);
            if (!key.SequenceEqual(storedKey)) return false;

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (entry.ExpiresAt > 0 && entry.ExpiresAt <= now)
            {
                entry = default;
                _count--;
                return false;
            }

            var expireMs = timestamp < 100_000_000_000L ? timestamp * 1000L : timestamp;
            if (expireMs <= now)
            {
                entry = default;
                _count--;
                return true;
            }
            
            entry.ExpiresAt = expireMs;
            return true;
        }
    }

    public int Ttl(ReadOnlySpan<byte> key)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);

        lock (_lockObj)
        {
            ref var entry = ref _index[bucket];
        
            if (entry.KeyLength == 0 || entry.KeyHash != hash || entry.KeyLength != key.Length) return -2;

            ReadOnlySpan<byte> storedKey = _dataBuffer.AsSpan(entry.KeyOffset, entry.KeyLength);
            if (!key.SequenceEqual(storedKey)) return -2;

            if (entry.ExpiresAt == 0) return -1;

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long remainderMs = entry.ExpiresAt - nowMs;

            if (remainderMs <= 0)
            {
                entry = default;
                _count--;
                return -2;
            }

            return (int)((remainderMs + 999) / 1000);
        }
    }

    public int Pttl(ReadOnlySpan<byte> key)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);

        lock (_lockObj)
        {
            ref var entry = ref _index[bucket];
        
            if (entry.KeyLength == 0 || entry.KeyHash != hash || entry.KeyLength != key.Length) return -2;

            ReadOnlySpan<byte> storedKey = _dataBuffer.AsSpan(entry.KeyOffset, entry.KeyLength);
            if (!key.SequenceEqual(storedKey)) return -2;

            if (entry.ExpiresAt == 0) return -1;

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long remainderMs = entry.ExpiresAt - nowMs;

            if (remainderMs <= 0)
            {
                entry = default;
                _count--;
                return -2;
            }

            return (int)remainderMs;
        }
    }

    public bool Persist(ReadOnlySpan<byte> key)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);

        lock (_lockObj)
        {
            ref var entry = ref _index[bucket];

            if (entry.KeyLength == 0 || entry.KeyHash != hash || entry.KeyLength != key.Length) return false;
            ReadOnlySpan<byte> storedKey = _dataBuffer.AsSpan(entry.KeyOffset, entry.KeyLength);
            if (!key.SequenceEqual(storedKey)) return false;

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (entry.ExpiresAt > 0 && entry.ExpiresAt <= now)
            {
                entry = default;
                _count--;
                return false;
            }

            if (entry.ExpiresAt == 0)
            {
                return false;
            }

            entry.ExpiresAt = 0;
            return true;
        }
    }
}