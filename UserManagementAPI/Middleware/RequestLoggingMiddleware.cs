using System.Diagnostics;

namespace UserManagementAPI.Middleware;

/// <summary>
/// Audit log of every request that reaches it: method, path and response status code.
/// Deliberately does not log the query string, headers (Authorization) or bodies, which can
/// contain secrets or personal data.
/// </summary>
public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var method = context.Request.Method;
        var path = LogSanitizer.Clean(context.Request.Path);
        logger.LogInformation("Request  {Method} {Path}", method, path);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            // The error-handling middleware above us writes the real response later, so at this
            // point Response.StatusCode is still the default 200. Record the status it will send.
            context.Response.StatusCode = ex is BadHttpRequestException bad
                ? bad.StatusCode
                : StatusCodes.Status500InternalServerError;
            throw;
        }
        finally
        {
            var status = context.Response.StatusCode;
            logger.Log(
                status >= 500 ? LogLevel.Error : LogLevel.Information,
                "Response {Method} {Path} -> {StatusCode} in {ElapsedMs} ms",
                method, path, status, stopwatch.ElapsedMilliseconds);
        }
    }
}
