namespace Extensions;

public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
        context.Response.Headers.TryAdd("X-Frame-Options", "SAMEORIGIN");
        context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
        context.Response.Headers.TryAdd("X-XSS-Protection", "0");
        context.Response.Headers.TryAdd(
            "Content-Security-Policy",
            "default-src 'self'; img-src 'self' data:; script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; font-src 'self' data:;");

        await next(context);
    }
}

