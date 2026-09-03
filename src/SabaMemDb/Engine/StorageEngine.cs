namespace SabaMemDb.Engine;

public partial class StorageEngine
{
    private readonly Entry[] _index = new Entry[10_000_000];
    
    private readonly byte[] _dataBuffer = new byte[256 * 1024 * 1024];
    
    private int _writeOffset = 0;
    private readonly ReaderWriterLockSlim _rwLock = new();
    
    private int _count;
}