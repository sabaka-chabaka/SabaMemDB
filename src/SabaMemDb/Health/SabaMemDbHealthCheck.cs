using Microsoft.Extensions.Diagnostics.HealthChecks;
using SabaMemDb.Engine;

namespace SabaMemDb.Health;

public class SabaMemDbHealthCheck(StorageEngine db) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var count = db.Count;
            var arrayAllocated = db.ArrayAllocated;
            var indexCapacity = db.IndexCapacity;
            var bufferCapacity = db.BufferCapacity;

            var data = new Dictionary<string, object>
            {
                { "count_of_entries", count },
                { "array_allocated_now", arrayAllocated },
                { "entries_count", count },
                { "array_allocated", arrayAllocated },
                { "index_capacity", indexCapacity },
                { "buffer_capacity", bufferCapacity }
            };

            var ping = db.Ping();
            if (ping != "Pong!")
            {
                return Task.FromResult(HealthCheckResult.Unhealthy("Storage engine ping failed", data: data));
            }

            var entryUsage = indexCapacity > 0 ? (double)count / indexCapacity : 0;
            var bufferUsage = bufferCapacity > 0 ? (double)arrayAllocated / bufferCapacity : 0;

            if (entryUsage >= 1.0 || bufferUsage >= 1.0)
            {
                return Task.FromResult(HealthCheckResult.Unhealthy(
                    $"Storage engine is full: entries {count}/{indexCapacity}, allocated {arrayAllocated}/{bufferCapacity} bytes",
                    data: data));
            }

            if (entryUsage >= 0.9 || bufferUsage >= 0.9)
            {
                return Task.FromResult(HealthCheckResult.Degraded(
                    $"Storage engine usage is high: entries {count}/{indexCapacity} ({(entryUsage * 100):F1}%), allocated {arrayAllocated}/{bufferCapacity} bytes ({(bufferUsage * 100):F1}%)",
                    data: data));
            }

            return Task.FromResult(HealthCheckResult.Healthy(
                $"Storage engine is healthy: entries {count}/{indexCapacity}, allocated {arrayAllocated}/{bufferCapacity} bytes",
                data: data));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Storage engine encountered an error", ex));
        }
    }
}
