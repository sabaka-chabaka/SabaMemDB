namespace SabaMemDb.Engine;

using System.Buffers.Text;

public partial class StorageEngine
{
    public bool Incr(ReadOnlySpan<byte> key)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);

        var start = (int)(hash % (ulong)_index.Length);

        var rwLock = GetLock(start);
        rwLock.EnterWriteLock();
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

            ReadOnlySpan<byte> storedValue = _dataBuffer.AsSpan(entry.ValueOffset, entry.ValueLength);
            if (!Utf8Parser.TryParse(storedValue, out long val, out int bytesConsumed) || bytesConsumed != storedValue.Length)
            {
                return false;
            }

            if (val == long.MaxValue)
            {
                return false;
            }

            Span<byte> formatted = stackalloc byte[32];
            if (!Utf8Formatter.TryFormat(val + 1, formatted, out int written))
            {
                return false;
            }

            if (_writeOffset + written > _dataBuffer.Length)
            {
                throw new InvalidOperationException("Not enough space in the buffer");
            }

            int vOffset = _writeOffset;
            formatted[..written].CopyTo(_dataBuffer.AsSpan(vOffset));
            _writeOffset += written;

            entry.ValueOffset = vOffset;
            entry.ValueLength = written;

            return true;
        }
        finally
        {
            rwLock.ExitWriteLock();
        }
    }
    
    public bool Decr(ReadOnlySpan<byte> key)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);

        var start = (int)(hash % (ulong)_index.Length);

        var rwLock = GetLock(start);
        rwLock.EnterWriteLock();
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

            ReadOnlySpan<byte> storedValue = _dataBuffer.AsSpan(entry.ValueOffset, entry.ValueLength);
            if (!Utf8Parser.TryParse(storedValue, out long val, out int bytesConsumed) || bytesConsumed != storedValue.Length)
            {
                return false;
            }

            if (val == long.MinValue)
            {
                return false;
            }

            Span<byte> formatted = stackalloc byte[32];
            if (!Utf8Formatter.TryFormat(val - 1, formatted, out int written))
            {
                return false;
            }

            if (_writeOffset + written > _dataBuffer.Length)
            {
                throw new InvalidOperationException("Not enough space in the buffer");
            }

            int vOffset = _writeOffset;
            formatted[..written].CopyTo(_dataBuffer.AsSpan(vOffset));
            _writeOffset += written;

            entry.ValueOffset = vOffset;
            entry.ValueLength = written;

            return true;
        }
        finally
        {
            rwLock.ExitWriteLock();
        }
    }

    public bool IncrBy(ReadOnlySpan<byte> key, long value)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);

        var start = (int)(hash % (ulong)_index.Length);

        var rwLock = GetLock(start);
        rwLock.EnterWriteLock();
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

            ReadOnlySpan<byte> storedValue = _dataBuffer.AsSpan(entry.ValueOffset, entry.ValueLength);
            if (!Utf8Parser.TryParse(storedValue, out long val, out int bytesConsumed) || bytesConsumed != storedValue.Length)
            {
                return false;
            }

            if ((value > 0 && val > long.MaxValue - value) || (value < 0 && val < long.MinValue - value))
            {
                return false;
            }

            Span<byte> formatted = stackalloc byte[32];
            if (!Utf8Formatter.TryFormat(val + value, formatted, out int written))
            {
                return false;
            }

            if (_writeOffset + written > _dataBuffer.Length)
            {
                throw new InvalidOperationException("Not enough space in the buffer");
            }

            int vOffset = _writeOffset;
            formatted[..written].CopyTo(_dataBuffer.AsSpan(vOffset));
            _writeOffset += written;

            entry.ValueOffset = vOffset;
            entry.ValueLength = written;

            return true;
        }
        finally
        {
            rwLock.ExitWriteLock();
        }
    }

    public bool DecrBy(ReadOnlySpan<byte> key, long value)
    {
        var hash = System.IO.Hashing.XxHash64.HashToUInt64(key);

        var start = (int)(hash % (ulong)_index.Length);

        var rwLock = GetLock(start);
        rwLock.EnterWriteLock();
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

            ReadOnlySpan<byte> storedValue = _dataBuffer.AsSpan(entry.ValueOffset, entry.ValueLength);
            if (!Utf8Parser.TryParse(storedValue, out long val, out int bytesConsumed) || bytesConsumed != storedValue.Length)
            {
                return false;
            }

            if ((value > 0 && val < long.MinValue + value) || (value < 0 && val > long.MaxValue + value))
            {
                return false;
            }

            Span<byte> formatted = stackalloc byte[32];
            if (!Utf8Formatter.TryFormat(val - value, formatted, out int written))
            {
                return false;
            }

            if (_writeOffset + written > _dataBuffer.Length)
            {
                throw new InvalidOperationException("Not enough space in the buffer");
            }

            int vOffset = _writeOffset;
            formatted[..written].CopyTo(_dataBuffer.AsSpan(vOffset));
            _writeOffset += written;

            entry.ValueOffset = vOffset;
            entry.ValueLength = written;

            return true;
        }
        finally
        {
            rwLock.ExitWriteLock();
        }
    }
}
