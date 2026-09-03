namespace SabaMemDb.Middleware;

public class AuthMiddleware(RequestDelegate next)
{
    private const string AuthHeaderName = "X-Auth-Password";
    
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api/db"))
        {
            await next(context);
            return;
        }
        
        if (context.Request.Headers.TryGetValue(AuthHeaderName, out var headerValues))
        {
            var passwordHash = headerValues.ToString();
            
            if (!string.IsNullOrEmpty(passwordHash))
            {
                if (passwordHash == Environment.GetEnvironmentVariable("PASSWORD"))
                {
                    await next(context);
                    return;
                }
            }
        }

        if (Environment.GetEnvironmentVariable("PASSWORD") == null)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"error\": \"ERR Authentication required.\"}");
    }
}