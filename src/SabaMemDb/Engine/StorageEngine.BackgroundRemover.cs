namespace SabaMemDb.Engine;

public partial class StorageEngine
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _cleanupTask;

    private async Task CleanupLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                RunCleanup();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void RunCleanup()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        _rwLock.EnterWriteLock();
        try
        {
            if (_count == 0) return;
            
            for (var i = 0; i < 50; i++) 
            {
                var bucket = Random.Shared.Next(0, _index.Length);
                ref var entry = ref _index[bucket];
                if (entry.KeyLength > 0 && entry.ExpiresAt > 0 && entry.ExpiresAt <= now) 
                {
                    DeleteEntryAt(bucket);
                }
            }
        }
        finally
        {
            _rwLock.ExitWriteLock();
        }
    }
}