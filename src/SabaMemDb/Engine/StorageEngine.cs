using SabaMemDb.Settings;

namespace SabaMemDb.Engine;

public partial class StorageEngine
{
    private readonly Entry[] _index;
    private readonly byte[] _dataBuffer;
    
    private int _writeOffset = 0;
    
    private int _count;

    public int Count => Volatile.Read(ref _count);
    
    private const int LockCount = 4096;

    private readonly ReaderWriterLockSlim[] _locks = CreateLocks();

    private static ReaderWriterLockSlim[] CreateLocks()
    {
        var locks = new ReaderWriterLockSlim[LockCount];

        for (var i = 0; i < LockCount; i++)
            locks[i] = new ReaderWriterLockSlim();

        return locks;
    }

    private ReaderWriterLockSlim GetLock(int bucket)
    {
        return _locks[bucket & (LockCount - 1)];
    }

    private void EnterWriteLock(int bucket1, int bucket2, out ReaderWriterLockSlim lock1, out ReaderWriterLockSlim? lock2)
    {
        var i1 = bucket1 & (LockCount - 1);
        var i2 = bucket2 & (LockCount - 1);

        if (i1 == i2)
        {
            lock1 = _locks[i1];
            lock2 = null;
            lock1.EnterWriteLock();
        }
        else if (i1 < i2)
        {
            lock1 = _locks[i1];
            lock2 = _locks[i2];
            lock1.EnterWriteLock();
            lock2.EnterWriteLock();
        }
        else
        {
            lock1 = _locks[i2];
            lock2 = _locks[i1];
            lock1.EnterWriteLock();
            lock2.EnterWriteLock();
        }
    }

    private static void ExitWriteLock(ReaderWriterLockSlim lock1, ReaderWriterLockSlim? lock2)
    {
        lock2?.ExitWriteLock();
        lock1.ExitWriteLock();
    }

    public StorageEngine(ISettings settings) : this(settings.MaxEntries, settings.BufferSize * 1024 * 1024)
    {
    }

    public StorageEngine(int indexCapacity, int bufferCapacity)
    {
        _index = new Entry[indexCapacity];
        _dataBuffer = new byte[bufferCapacity];
        _cleanupTask = Task.Run(() => CleanupLoopAsync(_cts.Token));
    }

    private int FindEntryIndex(ReadOnlySpan<byte> key, ulong hash)
    {
        var len = _index.Length;
        var start = (int)(hash % (ulong)len);

        for (var i = 0; i < len; i++)
        {
            var bucket = (start + i) % len;
            ref readonly var entry = ref _index[bucket];
            if (entry.KeyLength == 0)
            {
                return -1;
            }

            if (entry.KeyLength == key.Length && entry.KeyHash == hash)
            {
                ReadOnlySpan<byte> storedKey = _dataBuffer.AsSpan(entry.KeyOffset, entry.KeyLength);
                if (key.SequenceEqual(storedKey))
                {
                    return bucket;
                }
            }
        }

        return -1;
    }

    private int FindOrInsertIndex(ReadOnlySpan<byte> key, ulong hash, out bool exists)
    {
        var len = _index.Length;
        var start = (int)(hash % (ulong)len);

        for (var i = 0; i < len; i++)
        {
            var bucket = (start + i) % len;
            ref readonly var entry = ref _index[bucket];
            if (entry.KeyLength == 0)
            {
                exists = false;
                return bucket;
            }

            if (entry.KeyLength == key.Length && entry.KeyHash == hash)
            {
                ReadOnlySpan<byte> storedKey = _dataBuffer.AsSpan(entry.KeyOffset, entry.KeyLength);
                if (key.SequenceEqual(storedKey))
                {
                    exists = true;
                    return bucket;
                }
            }
        }

        throw new InvalidOperationException("Storage index is full");
    }

    private void DeleteEntryAt(int hole)
    {
        var len = _index.Length;
        var current = (hole + 1) % len;

        for (var step = 0; step < len; step++)
        {
            if (_index[current].KeyLength == 0)
            {
                break;
            }

            var itemHash = _index[current].KeyHash;
            var naturalBucket = (int)(itemHash % (ulong)len);

            var distToHole = (hole - naturalBucket + len) % len;
            var distToCurrent = (current - naturalBucket + len) % len;

            if (distToHole < distToCurrent)
            {
                _index[hole] = _index[current];
                hole = current;
            }

            current = (current + 1) % len;
        }

        _index[hole] = default;
        _count--;
    }
}