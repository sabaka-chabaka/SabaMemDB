namespace SabaMemDb.Engine;

public partial class StorageEngine : IDisposable
{
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _cleanupTask;

    public StorageEngine() 
    {
        _cleanupTask = Task.Run(() => CleanupLoopAsync(_cts.Token));
    }

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
            // Expected on dispose
        }
    }

    private void RunCleanup()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        lock (_lockObj)
        {
            if (_count == 0) return;
            
            for (var i = 0; i < 50; i++) 
            {
                var bucket = Random.Shared.Next(0, _index.Length);
                ref var entry = ref _index[bucket];
                if (entry.KeyLength > 0 && entry.ExpiresAt > 0 && entry.ExpiresAt <= now) 
                {
                    entry = default; 
                    _count--;
                }
            }
        }
    }
}