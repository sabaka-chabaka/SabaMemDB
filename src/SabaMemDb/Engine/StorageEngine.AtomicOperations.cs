namespace SabaMemDb.Engine;

using System.Buffers.Text;

public partial class StorageEngine : IDisposable
{
    public bool Incr(ReadOnlySpan<byte> key)
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
    }
    
    public bool Decr(ReadOnlySpan<byte> key)
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
    }

    public bool IncrBy(ReadOnlySpan<byte> key, long value)
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
    }

    public bool DecrBy(ReadOnlySpan<byte> key, long value)
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
    }
}
