namespace SabaMemDb.Engine;

using System;
using System.Buffers;

public class StorageEngine : IDisposable
{
    private readonly Entry[] _index = new Entry[10_000_000];
    
    private readonly byte[] _dataBuffer = ArrayPool<byte>.Shared.Rent(256 * 1024 * 1024);
    
    private int _writeOffset = 0;
    private readonly Lock _lockObj = new();

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

            _index[bucket] = new Entry
            {
                KeyHash = hash,
                KeyOffset = kOffset,
                KeyLength = key.Length,
                ValueOffset = vOffset,
                ValueLength = value.Length
            };
        }
    }
    
    public ReadOnlySpan<byte> Get(ReadOnlySpan<byte> key)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);
        
        var entry = _index[bucket];

        if (entry.KeyHash != hash) return ReadOnlySpan<byte>.Empty;
        ReadOnlySpan<byte> storedKey = _dataBuffer.AsSpan(entry.KeyOffset, entry.KeyLength);

        if (key.SequenceEqual(storedKey))
        {
            return _dataBuffer.AsSpan(entry.ValueOffset, entry.ValueLength);
        }

        return ReadOnlySpan<byte>.Empty;
    }

    public bool Delete(ReadOnlySpan<byte> key)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);
    
        lock (_lockObj)
        {
            var entry = _index[bucket];
        
            if (entry.KeyHash == hash)
            {
                ReadOnlySpan<byte> storedKey = _dataBuffer.AsSpan(entry.KeyOffset, entry.KeyLength);
            
                if (key.SequenceEqual(storedKey))
                {
                    _index[bucket] = default; 
                    return true;
                }
            }
        }
    
        return false;
    }

    public bool Exists(ReadOnlySpan<byte> key)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);
        var bucket = (int)(hash % (ulong)_index.Length);
        
        lock (_lockObj)
        {
            ref readonly var entry = ref _index[bucket];

            if (entry.KeyHash != hash || entry.KeyLength != key.Length) return false;
            ReadOnlySpan<byte> actualKey = _dataBuffer.AsSpan(entry.KeyOffset, entry.KeyLength);
            if (key.SequenceEqual(actualKey))
            {
                return true;
            }
        }
        return false;
    }

    public bool Rename(ReadOnlySpan<byte> oldKey, ReadOnlySpan<byte> newKey)
    {
        var oldHash = System.IO.Hashing.XxHash64.HashToUInt64(oldKey);
        var oldBucket = (int)(oldHash % (ulong)_index.Length);

        var newHash = System.IO.Hashing.XxHash64.HashToUInt64(newKey);
        var newBucket = (int)(newHash % (ulong)_index.Length);

        lock (_lockObj)
        {
            var oldEntry = _index[oldBucket];

            if (oldEntry.KeyHash != oldHash || oldEntry.KeyLength != oldKey.Length)
            {
                return false;
            }

            ReadOnlySpan<byte> storedOldKey = _dataBuffer.AsSpan(oldEntry.KeyOffset, oldEntry.KeyLength);
            if (!oldKey.SequenceEqual(storedOldKey))
            {
                return false;
            }

            if (_writeOffset + newKey.Length > _dataBuffer.Length)
            {
                throw new InvalidOperationException("Not enough space in the buffer");
            }

            var newKeyOffset = _writeOffset;
            newKey.CopyTo(_dataBuffer.AsSpan(newKeyOffset));
            _writeOffset += newKey.Length;

            _index[oldBucket] = default;

            _index[newBucket] = new Entry
            {
                KeyHash = newHash,
                KeyOffset = newKeyOffset,
                KeyLength = newKey.Length,
                ValueOffset = oldEntry.ValueOffset,
                ValueLength = oldEntry.ValueLength
            };

            return true;
        }
    }

    public bool RenameNotExists(ReadOnlySpan<byte> oldKey, ReadOnlySpan<byte> newKey)
    {
        return !Exists(newKey) && Rename(oldKey, newKey);
    }

    public void SetNotExists(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
    {
        if (Exists(key)) return;
        Set(key, value);
    }
    
    public void Dispose() => ArrayPool<byte>.Shared.Return(_dataBuffer);
}