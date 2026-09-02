namespace SabaMemDb.Engine;

using System;
using System.Buffers;

public partial class StorageEngine : IDisposable
{
    private readonly Entry[] _index = new Entry[10_000_000];
    
    private readonly byte[] _dataBuffer = ArrayPool<byte>.Shared.Rent(256 * 1024 * 1024);
    
    private int _writeOffset = 0;
    private readonly Lock _lockObj = new();
    
    private int _count = 0;
    
    public int Count => _count;
    
    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        ArrayPool<byte>.Shared.Return(_dataBuffer);
    }
}