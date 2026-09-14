using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SabaMemDb.Engine;
using SabaMemDb.Health;
using Xunit;

namespace SabaMemDB.Tests;

public class HealthCheckTests
{
    private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public async Task CheckHealthAsync_InitialState_ReturnsHealthyWithDiagnostics()
    {
        var db = new StorageEngine(100, 1024 * 1024);
        var healthCheck = new SabaMemDbHealthCheck(db);

        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data["count_of_entries"]);
        Assert.Equal(0, result.Data["array_allocated_now"]);
        Assert.Equal(0, result.Data["entries_count"]);
        Assert.Equal(0, result.Data["array_allocated"]);
        Assert.Equal(100, result.Data["index_capacity"]);
        Assert.Equal(1024 * 1024, result.Data["buffer_capacity"]);
    }

    [Fact]
    public async Task CheckHealthAsync_WithEntries_ReportsAccurateCountAndAllocation()
    {
        var db = new StorageEngine(100, 1024 * 1024);
        db.Set(B("testKey1"), B("testValue1"));
        db.Set(B("testKey2"), B("testValue2"));

        var healthCheck = new SabaMemDbHealthCheck(db);
        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Equal(2, result.Data["count_of_entries"]);
        Assert.True((int)result.Data["array_allocated_now"] > 0);
        Assert.Equal(db.ArrayAllocated, result.Data["array_allocated_now"]);
    }

    [Fact]
    public async Task CheckHealthAsync_HighEntriesUsage_ReturnsDegraded()
    {
        // 10 entries capacity, fill 9 entries (90%)
        var db = new StorageEngine(10, 1024 * 1024);
        for (int i = 0; i < 9; i++)
        {
            db.Set(B($"k{i}"), B($"v{i}"));
        }

        var healthCheck = new SabaMemDbHealthCheck(db);
        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(9, result.Data["count_of_entries"]);
    }

    [Fact]
    public async Task CheckHealthAsync_HighBufferUsage_ReturnsDegraded()
    {
        // 100 entries capacity, small buffer of 100 bytes
        var db = new StorageEngine(100, 100);
        // Write key and value that consume >= 90 bytes
        var val = new byte[80];
        db.Set(B("k0123456789"), val); // key length 11 + 80 = 91 bytes allocated >= 90%

        var healthCheck = new SabaMemDbHealthCheck(db);
        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.True((int)result.Data["array_allocated_now"] >= 90);
    }

    [Fact]
    public async Task HealthEndpoint_ViaHttp_Returns200OKAndValidJsonStructure()
    {
        var db = new StorageEngine(100, 1024 * 1024);
        db.Set(B("key1"), B("val1"));

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(db);
                services.AddHealthChecks().AddCheck<SabaMemDbHealthCheck>("saba_mem_db");
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/health", async (HealthCheckService healthCheckService, HttpContext context) =>
                    {
                        var report = await healthCheckService.CheckHealthAsync();
                        context.Response.StatusCode = report.Status == HealthStatus.Unhealthy
                            ? StatusCodes.Status503ServiceUnavailable
                            : StatusCodes.Status200OK;
                        await HealthCheckResponseWriter.WriteResponse(context, report);
                    });
                    endpoints.MapGet("/healthz", async (HealthCheckService healthCheckService, HttpContext context) =>
                    {
                        var report = await healthCheckService.CheckHealthAsync();
                        context.Response.StatusCode = report.Status == HealthStatus.Unhealthy
                            ? StatusCodes.Status503ServiceUnavailable
                            : StatusCodes.Status200OK;
                        await HealthCheckResponseWriter.WriteResponse(context, report);
                    });
                });
            });

        using var server = new TestServer(builder);
        using var client = server.CreateClient();

        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/json; charset=utf-8", response.Content.Headers.ContentType?.ToString());

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("Healthy", root.GetProperty("status").GetString());
        Assert.True(root.TryGetProperty("entries", out var entries));
        Assert.True(entries.TryGetProperty("saba_mem_db", out var dbEntry));
        Assert.Equal("Healthy", dbEntry.GetProperty("status").GetString());

        var data = dbEntry.GetProperty("data");
        Assert.Equal(1, data.GetProperty("count_of_entries").GetInt32());
        Assert.True(data.GetProperty("array_allocated_now").GetInt32() > 0);
        Assert.Equal(100, data.GetProperty("index_capacity").GetInt32());
        Assert.Equal(1024 * 1024, data.GetProperty("buffer_capacity").GetInt32());

        // Also test /healthz
        var healthzResponse = await client.GetAsync("/healthz");
        Assert.Equal(HttpStatusCode.OK, healthzResponse.StatusCode);
    }

    [Fact]
    public async Task CheckHealthAsync_FullEntries_ReturnsUnhealthy()
    {
        var db = new StorageEngine(2, 1024 * 1024);
        db.Set(B("k1"), B("v1"));
        db.Set(B("k2"), B("v2"));

        var healthCheck = new SabaMemDbHealthCheck(db);
        var context = new HealthCheckContext();
        var result = await healthCheck.CheckHealthAsync(context);

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.Equal(2, result.Data["count_of_entries"]);
    }

    [Fact]
    public async Task HealthEndpoint_Unhealthy_Returns503ServiceUnavailable()
    {
        var db = new StorageEngine(2, 1024 * 1024);
        db.Set(B("k1"), B("v1"));
        db.Set(B("k2"), B("v2"));

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(db);
                services.AddHealthChecks().AddCheck<SabaMemDbHealthCheck>("saba_mem_db");
                services.AddRouting();
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapGet("/health", async (HealthCheckService healthCheckService, HttpContext context) =>
                    {
                        var report = await healthCheckService.CheckHealthAsync();
                        context.Response.StatusCode = report.Status == HealthStatus.Unhealthy
                            ? StatusCodes.Status503ServiceUnavailable
                            : StatusCodes.Status200OK;
                        await HealthCheckResponseWriter.WriteResponse(context, report);
                    });
                });
            });

        using var server = new TestServer(builder);
        using var client = server.CreateClient();

        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("Unhealthy", root.GetProperty("status").GetString());
    }
}
