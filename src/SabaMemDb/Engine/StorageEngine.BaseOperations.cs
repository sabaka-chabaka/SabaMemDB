namespace SabaMemDb.Engine;

public partial class StorageEngine
{
    public void Set(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);

        lock (_lockObj)
        {
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

            ref var existing = ref _index[bucket];
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            var wasActive = existing.KeyLength > 0 && (existing.ExpiresAt == 0 || existing.ExpiresAt > now);
            if (!wasActive)
            {
                _count++;
            }

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
    }
    
    public ReadOnlySpan<byte> Get(ReadOnlySpan<byte> key)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);

        lock (_lockObj)
        {
            ref var entry = ref _index[bucket];

            if (entry.KeyLength == 0 || entry.KeyHash != hash || entry.KeyLength != key.Length) return ReadOnlySpan<byte>.Empty;
            ReadOnlySpan<byte> storedKey = _dataBuffer.AsSpan(entry.KeyOffset, entry.KeyLength);

            if (!key.SequenceEqual(storedKey)) return ReadOnlySpan<byte>.Empty;

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (entry.ExpiresAt > 0 && entry.ExpiresAt <= now)
            {
                entry = default;
                _count--;
                return ReadOnlySpan<byte>.Empty;
            }

            return _dataBuffer.AsSpan(entry.ValueOffset, entry.ValueLength);
        }
    }

    public bool Delete(ReadOnlySpan<byte> key)
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

            entry = default; 
            _count--;
            return true;
        }
    }

    public bool Exists(ReadOnlySpan<byte> key)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);
        
        lock (_lockObj)
        {
            ref var entry = ref _index[bucket];

            if (entry.KeyLength == 0 || entry.KeyHash != hash || entry.KeyLength != key.Length) return false;
            ReadOnlySpan<byte> actualKey = _dataBuffer.AsSpan(entry.KeyOffset, entry.KeyLength);
            if (!key.SequenceEqual(actualKey)) return false;

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (entry.ExpiresAt > 0 && entry.ExpiresAt <= now)
            {
                entry = default;
                _count--;
                return false;
            }

            return true;
        }
    }

    public bool Rename(ReadOnlySpan<byte> oldKey, ReadOnlySpan<byte> newKey)
    {
        var oldHash = System.IO.Hashing.XxHash64.HashToUInt64(oldKey);
        var oldBucket = (int)(oldHash % (ulong)_index.Length);

        var newHash = System.IO.Hashing.XxHash64.HashToUInt64(newKey);
        var newBucket = (int)(newHash % (ulong)_index.Length);

        lock (_lockObj)
        {
            ref var oldEntry = ref _index[oldBucket];

            if (oldEntry.KeyLength == 0 || oldEntry.KeyHash != oldHash || oldEntry.KeyLength != oldKey.Length)
            {
                return false;
            }

            ReadOnlySpan<byte> storedOldKey = _dataBuffer.AsSpan(oldEntry.KeyOffset, oldEntry.KeyLength);
            if (!oldKey.SequenceEqual(storedOldKey))
            {
                return false;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (oldEntry.ExpiresAt > 0 && oldEntry.ExpiresAt <= now)
            {
                oldEntry = default;
                _count--;
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

            if (oldBucket != newBucket)
            {
                ref var newEntry = ref _index[newBucket];
                var newEntryWasActive = newEntry.KeyLength > 0 && (newEntry.ExpiresAt == 0 || newEntry.ExpiresAt > now);
                if (newEntryWasActive)
                {
                    _count--;
                }
                oldEntry = default;
            }

            var newKeyOffset = _writeOffset;
            newKey.CopyTo(_dataBuffer.AsSpan(newKeyOffset));
            _writeOffset += newKey.Length;

            _index[newBucket] = new Entry
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
    }

    public bool RenameNotExists(ReadOnlySpan<byte> oldKey, ReadOnlySpan<byte> newKey)
    {
        if (oldKey.SequenceEqual(newKey)) return false;

        var oldHash = System.IO.Hashing.XxHash64.HashToUInt64(oldKey);
        var oldBucket = (int)(oldHash % (ulong)_index.Length);

        var newHash = System.IO.Hashing.XxHash64.HashToUInt64(newKey);
        var newBucket = (int)(newHash % (ulong)_index.Length);

        lock (_lockObj)
        {
            ref var oldEntry = ref _index[oldBucket];

            if (oldEntry.KeyLength == 0 || oldEntry.KeyHash != oldHash || oldEntry.KeyLength != oldKey.Length)
            {
                return false;
            }

            ReadOnlySpan<byte> storedOldKey = _dataBuffer.AsSpan(oldEntry.KeyOffset, oldEntry.KeyLength);
            if (!oldKey.SequenceEqual(storedOldKey))
            {
                return false;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (oldEntry.ExpiresAt > 0 && oldEntry.ExpiresAt <= now)
            {
                oldEntry = default;
                _count--;
                return false;
            }

            ref var newEntry = ref _index[newBucket];
            if (newEntry.KeyLength > 0)
            {
                if (newEntry.ExpiresAt > 0 && newEntry.ExpiresAt <= now)
                {
                    newEntry = default;
                    _count--;
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

            var savedOldEntry = oldEntry;
            oldEntry = default;

            var newKeyOffset = _writeOffset;
            newKey.CopyTo(_dataBuffer.AsSpan(newKeyOffset));
            _writeOffset += newKey.Length;

            _index[newBucket] = new Entry
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
    }

    public bool SetNotExists(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);

        lock (_lockObj)
        {
            ref var entry = ref _index[bucket];
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            if (entry.KeyLength > 0)
            {
                if (entry.KeyHash == hash && entry.KeyLength == key.Length && key.SequenceEqual(_dataBuffer.AsSpan(entry.KeyOffset, entry.KeyLength)))
                {
                    if (entry.ExpiresAt > 0 && entry.ExpiresAt <= now)
                    {
                        entry = default;
                        _count--;
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (entry.ExpiresAt > 0 && entry.ExpiresAt <= now)
                {
                    entry = default;
                    _count--;
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

            var wasActive = entry.KeyLength > 0 && (entry.ExpiresAt == 0 || entry.ExpiresAt > now);
            if (!wasActive)
            {
                _count++;
            }

            entry = new Entry
            {
                KeyHash = hash,
                KeyOffset = kOffset,
                KeyLength = key.Length,
                ValueOffset = vOffset,
                ValueLength = value.Length,
                ExpiresAt = 0
            };

            return true;
        }
    }
}