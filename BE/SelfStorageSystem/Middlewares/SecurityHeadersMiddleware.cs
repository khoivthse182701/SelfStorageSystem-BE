using Microsoft.AspNetCore.Http;

namespace SelfStorageSystem.Middlewares;

public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 1. Prevent MIME-sniffing
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

        // 2. Clickjacking protection
        context.Response.Headers.Append("X-Frame-Options", "DENY");

        // 3. XSS protection filter
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

        // 4. Referrer Policy: avoid leaking sensitive URLs/tokens in referrer header
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        // 5. Ensure sensitive Auth responses are never cached by proxies or browsers
        if (context.Request.Path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.Headers.Append("Cache-Control", "no-store, no-cache, must-revalidate");
            context.Response.Headers.Append("Pragma", "no-cache");
        }

        // Remove server info headers
        context.Response.Headers.Remove("Server");
        context.Response.Headers.Remove("X-Powered-By");

        await _next(context);
    }
}
