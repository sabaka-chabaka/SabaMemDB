namespace SabaMemDb.Engine;

public partial class StorageEngine 
{
    public bool Expire(ReadOnlySpan<byte> key, int seconds)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        
        _rwLock.EnterWriteLock();
        try
        {
            var bucket = FindEntryIndex(key, hash);
            if (bucket < 0) return false;

            ref var entry = ref _index[bucket];
            
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (entry.ExpiresAt > 0 && entry.ExpiresAt <= now)
            {
                DeleteEntryAt(bucket);
                return false;
            }

            if (seconds <= 0)
            {
                DeleteEntryAt(bucket);
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
        
        _rwLock.EnterWriteLock();
        try
        {
            var bucket = FindEntryIndex(key, hash);
            if (bucket < 0) return false;

            ref var entry = ref _index[bucket];
            
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (entry.ExpiresAt > 0 && entry.ExpiresAt <= now)
            {
                DeleteEntryAt(bucket);
                return false;
            }

            if (milliseconds <= 0)
            {
                DeleteEntryAt(bucket);
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
        
        _rwLock.EnterWriteLock();
        try
        {
            var bucket = FindEntryIndex(key, hash);
            if (bucket < 0) return false;

            ref var entry = ref _index[bucket];
            
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (entry.ExpiresAt > 0 && entry.ExpiresAt <= now)
            {
                DeleteEntryAt(bucket);
                return false;
            }

            var expireMs = timestamp < 100_000_000_000L ? timestamp * 1000L : timestamp;
            if (expireMs <= now)
            {
                DeleteEntryAt(bucket);
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

        _rwLock.EnterReadLock();
        try
        {
            var bucket = FindEntryIndex(key, hash);
            if (bucket < 0) return -2;

            ref readonly var entry = ref _index[bucket];
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

        _rwLock.EnterReadLock();
        try
        {
            var bucket = FindEntryIndex(key, hash);
            if (bucket < 0) return -2;

            ref readonly var entry = ref _index[bucket];
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

        _rwLock.EnterWriteLock();
        try
        {
            var bucket = FindEntryIndex(key, hash);
            if (bucket < 0) return false;

            ref var entry = ref _index[bucket];

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (entry.ExpiresAt > 0 && entry.ExpiresAt <= now)
            {
                DeleteEntryAt(bucket);
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
