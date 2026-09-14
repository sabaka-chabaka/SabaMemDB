using System.Net;
using System.Text;
using SabaMemDb.Client;
using Xunit;

namespace SabaMemDB.Tests;

public class SMDBClientTests
{
    private class MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    [Fact]
    public async Task Set_StringValue_SendsCorrectRequest()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new SMDBClient(httpClient, "http://localhost:5000", "secret");

        var success = await client.Set("myKey", "myValue");

        Assert.True(success);
        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("http://localhost:5000/api/db/set/myKey", capturedRequest.RequestUri?.ToString());
        Assert.Equal("secret", capturedRequest.Headers.GetValues("X-Auth-Password").First());
    }

    [Fact]
    public async Task Set_ByteArrayValue_SendsCorrectRequest()
    {
        HttpRequestMessage? capturedRequest = null;
        var handler = new MockHttpMessageHandler(req =>
        {
            capturedRequest = req;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new SMDBClient(httpClient, "localhost:5000", "secret");

        var success = await client.Set("myKey", Encoding.UTF8.GetBytes("myBytes"));

        Assert.True(success);
        Assert.NotNull(capturedRequest);
        Assert.Equal("http://localhost:5000/api/db/set/myKey", capturedRequest.RequestUri?.ToString());
    }

    [Fact]
    public async Task SetNotExists_ReturnsExpectedResult()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString().Contains("existingKey") == true)
                return new HttpResponseMessage(HttpStatusCode.Conflict);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new SMDBClient(httpClient, "http://localhost:5000");

        Assert.True(await client.SetNotExists("newKey", "val"));
        Assert.False(await client.SetNotExists("existingKey", "val"));
        Assert.True(await client.SetNx("newKey2", "val"));
        Assert.True(await client.SetNotExists("newKeyBytes", Encoding.UTF8.GetBytes("val")));
        Assert.True(await client.SetNx("newKeyBytes2", Encoding.UTF8.GetBytes("val")));
    }

    [Fact]
    public async Task Get_ReturnsStringOrNull()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString().EndsWith("/key1") == true)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("found_value") };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new SMDBClient(httpClient, "http://localhost:5000");

        var val1 = await client.Get("key1");
        var val2 = await client.Get("missing");

        Assert.Equal("found_value", val1);
        Assert.Null(val2);
    }

    [Fact]
    public async Task GetBytes_ReturnsBytesOrNull()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString().EndsWith("/key1") == true)
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes("byte_val")) };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new SMDBClient(httpClient, "http://localhost:5000");

        var val1 = await client.GetBytes("key1");
        var val2 = await client.GetBytes("missing");

        Assert.Equal(Encoding.UTF8.GetBytes("byte_val"), val1);
        Assert.Null(val2);
    }

    [Fact]
    public async Task Delete_ReturnsExpectedResult()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString().EndsWith("/key1") == true)
                return new HttpResponseMessage(HttpStatusCode.OK);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new SMDBClient(httpClient, "http://localhost:5000");

        Assert.True(await client.Delete("key1"));
        Assert.False(await client.Delete("key2"));
    }

    [Fact]
    public async Task Exists_ReturnsExpectedResult()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString().EndsWith("/key1") == true)
                return new HttpResponseMessage(HttpStatusCode.OK);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new SMDBClient(httpClient, "http://localhost:5000");

        Assert.True(await client.Exists("key1"));
        Assert.False(await client.Exists("key2"));
    }

    [Fact]
    public async Task Rename_And_RenameNotExists_ReturnsExpectedResult()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri?.ToString().Contains("fail") == true)
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new SMDBClient(httpClient, "http://localhost:5000");

        Assert.True(await client.Rename("oldKey", "newKey"));
        Assert.False(await client.Rename("failKey", "newKey"));

        Assert.True(await client.RenameNotExists("oldKey", "newKey"));
        Assert.True(await client.RenameNx("oldKey", "newKey"));
    }

    [Fact]
    public async Task ExpirationMethods_WorkAsExpected()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            var uri = req.RequestUri?.ToString() ?? "";
            if (uri.Contains("/ttl/"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("15") };
            if (uri.Contains("/pttl/"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("15000") };
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new SMDBClient(httpClient, "http://localhost:5000");

        Assert.True(await client.Expire("k", 10));
        Assert.True(await client.PExpire("k", 1000));
        Assert.True(await client.ExpireAt("k", 123456789));
        Assert.True(await client.Persist("k"));
        Assert.Equal(15, await client.Ttl("k"));
        Assert.Equal(15000, await client.Pttl("k"));
    }

    [Fact]
    public async Task AtomicOperations_And_Ping_WorkAsExpected()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            var uri = req.RequestUri?.ToString() ?? "";
            if (uri.Contains("/ping/"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("Pong!") };
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new SMDBClient(httpClient, "http://localhost:5000");

        Assert.True(await client.Incr("counter"));
        Assert.True(await client.Decr("counter"));
        Assert.True(await client.IncrBy("counter", 5));
        Assert.True(await client.DecrBy("counter", 5));
        Assert.Equal("Pong!", await client.Ping());
    }

    [Fact]
    public async Task HealthCheckMethods_WorkAsExpected()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            var uri = req.RequestUri?.ToString() ?? "";
            if (uri.Contains("/health"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"status\":\"Healthy\"}") };
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new SMDBClient(httpClient, "http://localhost:5000");

        Assert.True(await client.IsHealthyAsync());
        var healthJson = await client.GetHealthAsync();
        Assert.NotNull(healthJson);
        Assert.Contains("Healthy", healthJson);
    }

    [Fact]
    public async Task HealthCheckMethods_HandleUnhealthyStatus()
    {
        var handler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        });

        using var httpClient = new HttpClient(handler);
        using var client = new SMDBClient(httpClient, "http://localhost:5000");

        Assert.False(await client.IsHealthyAsync());
        var healthJson = await client.GetHealthAsync();
        Assert.Null(healthJson);
    }

    [Fact]
    public void Constructors_HandleHostAndDispose()
    {
        using var c1 = new SMDBClient("http://myhost:1234/", "p1");
        using var c2 = new SMDBClient("https://myhost:1234/", "p1");
        using var c3 = new SMDBClient("myhost:1234", "p1");
        using var c4 = new SMDBClient(new HttpClient(), "myhost:1234");
    }
}
