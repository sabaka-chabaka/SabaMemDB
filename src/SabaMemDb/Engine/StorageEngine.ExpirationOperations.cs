namespace SabaMemDb.Engine;

public partial class StorageEngine 
{
    public bool Expire(ReadOnlySpan<byte> key, int seconds)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);
        
        _rwLock.EnterWriteLock();
        try
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
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }
    
    public bool PExpire(ReadOnlySpan<byte> key, int milliseconds)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);
        
        _rwLock.EnterWriteLock();
        try
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
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }
    
    public bool ExpireAt(ReadOnlySpan<byte> key, long timestamp)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);
        
        _rwLock.EnterWriteLock();
        try
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
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }

    public int Ttl(ReadOnlySpan<byte> key)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);

        _rwLock.EnterReadLock();
        try
        {
            ref readonly var entry = ref _index[bucket];
        
            if (entry.KeyLength == 0 || entry.KeyHash != hash || entry.KeyLength != key.Length) return -2;

            ReadOnlySpan<byte> storedKey = _dataBuffer.AsSpan(entry.KeyOffset, entry.KeyLength);
            if (!key.SequenceEqual(storedKey)) return -2;

            if (entry.ExpiresAt == 0) return -1;

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long remainderMs = entry.ExpiresAt - nowMs;

            if (remainderMs <= 0)
            {
                return -2;
            }

            return (int)((remainderMs + 999) / 1000);
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public int Pttl(ReadOnlySpan<byte> key)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);

        _rwLock.EnterReadLock();
        try
        {
            ref readonly var entry = ref _index[bucket];
        
            if (entry.KeyLength == 0 || entry.KeyHash != hash || entry.KeyLength != key.Length) return -2;

            ReadOnlySpan<byte> storedKey = _dataBuffer.AsSpan(entry.KeyOffset, entry.KeyLength);
            if (!key.SequenceEqual(storedKey)) return -2;

            if (entry.ExpiresAt == 0) return -1;

            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long remainderMs = entry.ExpiresAt - nowMs;

            if (remainderMs <= 0)
            {
                return -2;
            }

            return (int)remainderMs;
        }
        finally
        {
            _rwLock.ExitReadLock();
        }
    }

    public bool Persist(ReadOnlySpan<byte> key)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);

        _rwLock.EnterWriteLock();
        try
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
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }
}
