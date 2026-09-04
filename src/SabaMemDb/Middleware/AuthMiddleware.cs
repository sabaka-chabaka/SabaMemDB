using SabaMemDb.Settings;

namespace SabaMemDb.Middleware;

public class AuthMiddleware(RequestDelegate next, ILogger<AuthMiddleware> logger, ISettings settings)
{
    private const string AuthHeaderName = "X-Auth-Password";
    private readonly string _password = settings.Password;
    
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/db"))
        {
            await next(context);
            logger.LogInformation("Request path ahead of base db path: {Path}", context.Request.Path);
            return;
        }
        
        if (context.Request.Headers.TryGetValue(AuthHeaderName, out var headerValues))
        {
            var passwordHash = headerValues.ToString();
            
            if (!string.IsNullOrEmpty(passwordHash))
            {
                if (passwordHash == _password)
                {
                    await next(context);
                    return;
                }
            }
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        logger.LogWarning("Authentication failed for {Path}", context.Request.Path);
        await context.Response.WriteAsync("{\"error\": \"ERR Authentication required.\"}");
    }
}