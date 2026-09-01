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
    
    public void Dispose() => ArrayPool<byte>.Shared.Return(_dataBuffer);
}