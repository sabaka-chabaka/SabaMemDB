namespace SabaMemDb.Engine;

public partial class StorageEngine
{
    public void Set(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);

        var start = (int)(hash % (ulong)_index.Length);

        var rwLock = GetLock(start);
        rwLock.EnterWriteLock();
        
        try
        {
            if (_writeOffset + key.Length + value.Length > _dataBuffer.Length)
            {
                throw new InvalidOperationException("Not enough space in the buffer");
            }

            var bucket = FindOrInsertIndex(key, hash, out var exists);
            ref var existing = ref _index[bucket];
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var wasActive = exists && (existing.ExpiresAt == 0 || existing.ExpiresAt > now);
            if (!wasActive)
            {
                _count++;
            }

            var kOffset = _writeOffset;
            key.CopyTo(_dataBuffer.AsSpan(kOffset));
            _writeOffset += key.Length;
            
            int vOffset = _writeOffset;
            value.CopyTo(_dataBuffer.AsSpan(vOffset));
            _writeOffset += value.Length;

            existing = new Entry
            {
                KeyHash = hash,
                KeyOffset = kOffset,
                KeyLength = key.Length,
                ValueOffset = vOffset,
                ValueLength = value.Length,
                ExpiresAt = 0
            };
        }
        finally
        {
            rwLock.ExitWriteLock();
        }
    }
    
    public ReadOnlySpan<byte> Get(ReadOnlySpan<byte> key)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);

        var start = (int)(hash % (ulong)_index.Length);

        var rwLock = GetLock(start);
        rwLock.EnterReadLock();
        try
        {
            var bucket = FindEntryIndex(key, hash);
            if (bucket < 0) return ReadOnlySpan<byte>.Empty;

            ref readonly var entry = ref _index[bucket];

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (entry.ExpiresAt > 0 && entry.ExpiresAt <= now)
            {
                return ReadOnlySpan<byte>.Empty;
            }

            return _dataBuffer.AsSpan(entry.ValueOffset, entry.ValueLength);
        }
        finally
        {
            rwLock.ExitReadLock();
        }
    }

    public bool Delete(ReadOnlySpan<byte> key)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);

        var start = (int)(hash % (ulong)_index.Length);

        var rwLock = GetLock(start);
        rwLock.EnterWriteLock();
        try
        {
            var bucket = FindEntryIndex(key, hash);
            if (bucket < 0) return false;

            ref readonly var entry = ref _index[bucket];
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var isExpired = entry.ExpiresAt > 0 && entry.ExpiresAt <= now;

            DeleteEntryAt(bucket);
            return !isExpired;
        }
        finally
        {
            rwLock.ExitWriteLock();
        }
    }

    public bool Exists(ReadOnlySpan<byte> key)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        
        var start = (int)(hash % (ulong)_index.Length);

        var rwLock = GetLock(start);
        rwLock.EnterReadLock();
        try
        {
            var bucket = FindEntryIndex(key, hash);
            if (bucket < 0) return false;

            ref readonly var entry = ref _index[bucket];
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (entry.ExpiresAt > 0 && entry.ExpiresAt <= now)
            {
                return false;
            }

            return true;
        }
        finally
        {
            rwLock.ExitReadLock();
        }
    }

    public bool Rename(ReadOnlySpan<byte> oldKey, ReadOnlySpan<byte> newKey)
    {
        var oldHash = System.IO.Hashing.XxHash64.HashToUInt64(oldKey);
        var newHash = System.IO.Hashing.XxHash64.HashToUInt64(newKey);

        var oldStart = (int)(oldHash % (ulong)_index.Length);
        var newStart = (int)(newHash % (ulong)_index.Length);

        EnterWriteLock(oldStart, newStart, out var lock1, out var lock2);
        try
        {
            var oldBucket = FindEntryIndex(oldKey, oldHash);
            if (oldBucket < 0)
            {
                return false;
            }

            ref readonly var oldEntry = ref _index[oldBucket];
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (oldEntry.ExpiresAt > 0 && oldEntry.ExpiresAt <= now)
            {
                DeleteEntryAt(oldBucket);
                return false;
            }

            if (oldKey.SequenceEqual(newKey))
            {
                return true;
            }

            if (_writeOffset + newKey.Length > _dataBuffer.Length)
            {
                throw new InvalidOperationException("Not enough space in the buffer");
            }

            var savedOldEntry = oldEntry;
            DeleteEntryAt(oldBucket);

            var newKeyOffset = _writeOffset;
            newKey.CopyTo(_dataBuffer.AsSpan(newKeyOffset));
            _writeOffset += newKey.Length;

            var newBucket = FindOrInsertIndex(newKey, newHash, out var exists);
            ref var newEntry = ref _index[newBucket];

            if (!exists)
            {
                _count++;
            }

            newEntry = new Entry
            {
                KeyHash = newHash,
                KeyOffset = newKeyOffset,
                KeyLength = newKey.Length,
                ValueOffset = savedOldEntry.ValueOffset,
                ValueLength = savedOldEntry.ValueLength,
                ExpiresAt = savedOldEntry.ExpiresAt
            };

            return true;
        }
        finally
        {
            ExitWriteLock(lock1, lock2);
        }
    }

    public bool RenameNotExists(ReadOnlySpan<byte> oldKey, ReadOnlySpan<byte> newKey)
    {
        if (oldKey.SequenceEqual(newKey)) return false;

        var oldHash = System.IO.Hashing.XxHash64.HashToUInt64(oldKey);
        var newHash = System.IO.Hashing.XxHash64.HashToUInt64(newKey);

        var oldStart = (int)(oldHash % (ulong)_index.Length);
        var newStart = (int)(newHash % (ulong)_index.Length);

        EnterWriteLock(oldStart, newStart, out var lock1, out var lock2);
        try
        {
            var oldBucket = FindEntryIndex(oldKey, oldHash);
            if (oldBucket < 0)
            {
                return false;
            }

            ref readonly var oldEntry = ref _index[oldBucket];
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (oldEntry.ExpiresAt > 0 && oldEntry.ExpiresAt <= now)
            {
                DeleteEntryAt(oldBucket);
                return false;
            }

            var newBucket = FindEntryIndex(newKey, newHash);
            if (newBucket >= 0)
            {
                ref readonly var existingNew = ref _index[newBucket];
                if (existingNew.ExpiresAt > 0 && existingNew.ExpiresAt <= now)
                {
                    DeleteEntryAt(newBucket);
                    oldBucket = FindEntryIndex(oldKey, oldHash);
                    if (oldBucket < 0) return false;
                }
                else
                {
                    return false;
                }
            }

            if (_writeOffset + newKey.Length > _dataBuffer.Length)
            {
                throw new InvalidOperationException("Not enough space in the buffer");
            }

            var savedOldEntry = _index[oldBucket];
            DeleteEntryAt(oldBucket);

            var newKeyOffset = _writeOffset;
            newKey.CopyTo(_dataBuffer.AsSpan(newKeyOffset));
            _writeOffset += newKey.Length;

            var insertBucket = FindOrInsertIndex(newKey, newHash, out _);
            _index[insertBucket] = new Entry
            {
                KeyHash = newHash,
                KeyOffset = newKeyOffset,
                KeyLength = newKey.Length,
                ValueOffset = savedOldEntry.ValueOffset,
                ValueLength = savedOldEntry.ValueLength,
                ExpiresAt = savedOldEntry.ExpiresAt
            };
            _count++;

            return true;
        }
        finally
        {
            ExitWriteLock(lock1, lock2);
        }
    }

    public bool SetNotExists(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);

        var start = (int)(hash % (ulong)_index.Length);

        var rwLock = GetLock(start);
        rwLock.EnterWriteLock();
        try
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var bucket = FindOrInsertIndex(key, hash, out var exists);

            if (exists)
            {
                ref readonly var entry = ref _index[bucket];
                if (entry.ExpiresAt > 0 && entry.ExpiresAt <= now)
                {
                    DeleteEntryAt(bucket);
                    bucket = FindOrInsertIndex(key, hash, out _);
                }
                else
                {
                    return false;
                }
            }

            if (_writeOffset + key.Length + value.Length > _dataBuffer.Length)
            {
                throw new InvalidOperationException("Not enough space in the buffer");
            }

            var kOffset = _writeOffset;
            key.CopyTo(_dataBuffer.AsSpan(kOffset));
            _writeOffset += key.Length;
            
            int vOffset = _writeOffset;
            value.CopyTo(_dataBuffer.AsSpan(vOffset));
            _writeOffset += value.Length;

            _index[bucket] = new Entry
            {
                KeyHash = hash,
                KeyOffset = kOffset,
                KeyLength = key.Length,
                ValueOffset = vOffset,
                ValueLength = value.Length,
                ExpiresAt = 0
            };
            _count++;

            return true;
        }
        finally
        {
            rwLock.ExitWriteLock();
        }
    }
}
