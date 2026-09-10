using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using SabaMemDb.Middleware;
using SabaMemDb.Settings;
using Xunit;

namespace SabaMemDB.Tests;

public class AuthMiddlewareTests
{
    private readonly ISettings _settings = new Settings("test-password-123", 1000, 10);
    private readonly NullLogger<AuthMiddleware> _logger = NullLogger<AuthMiddleware>.Instance;

    [Fact]
    public async Task InvokeAsync_NonDbPath_BypassesAuth()
    {
        bool nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next, _logger, _settings);
        var context = new DefaultHttpContext();
        context.Request.Path = "/health";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_DbPath_ValidPassword_CallsNext()
    {
        bool nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next, _logger, _settings);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/db/get/test";
        context.Request.Headers["X-Auth-Password"] = "test-password-123";

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_DbPath_MissingHeader_Returns401Unauthorized()
    {
        bool nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next, _logger, _settings);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/db/get/test";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Equal("application/json", context.Response.ContentType);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var responseText = await reader.ReadToEndAsync();
        Assert.Contains("ERR Authentication required", responseText);
    }

    [Fact]
    public async Task InvokeAsync_DbPath_WrongPassword_Returns401Unauthorized()
    {
        bool nextCalled = false;
        RequestDelegate next = _ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new AuthMiddleware(next, _logger, _settings);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/db/get/test";
        context.Request.Headers["X-Auth-Password"] = "wrong-password";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }
}
