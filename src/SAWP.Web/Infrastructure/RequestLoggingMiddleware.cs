using System.Diagnostics;

namespace SAWP.Web.Infrastructure;

/// <summary>
/// Middleware to log each incoming HTTP request with timing, status code, and user identity.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var path = context.Request.Path;
        var method = context.Request.Method;

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            var statusCode = context.Response.StatusCode;
            var user = context.User.Identity?.IsAuthenticated == true
                ? context.User.Identity.Name
                : "Anonymous";

            if (!path.StartsWithSegments("/css") && !path.StartsWithSegments("/js") && !path.StartsWithSegments("/lib") && !path.StartsWithSegments("/img"))
            {
                _logger.LogInformation(
                    "[HTTP] {Method} {Path} responded {StatusCode} in {ElapsedMs}ms (User={User})",
                    method, path, statusCode, sw.ElapsedMilliseconds, user);
            }
        }
    }
}
