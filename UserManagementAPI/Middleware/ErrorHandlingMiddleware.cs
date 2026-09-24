using Microsoft.AspNetCore.Mvc;

namespace UserManagementAPI.Middleware;

/// <summary>
/// Outermost middleware: catches any exception escaping the rest of the pipeline and returns
/// the standard JSON error body, so clients never see a stack trace or a dropped connection.
/// </summary>
public class ErrorHandlingMiddleware(
    RequestDelegate next,
    IProblemDetailsService problemDetails,
    ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            var problem = ex is BadHttpRequestException badRequest
                // Client error (malformed JSON, wrong types, empty body, ...): not a server fault.
                ? new ProblemDetails { Status = badRequest.StatusCode, Title = "Invalid request.", Detail = "The request could not be read. Check the body and parameters." }
                : new ProblemDetails { Status = StatusCodes.Status500InternalServerError, Title = "Internal server error." };

            if (problem.Status == StatusCodes.Status500InternalServerError)
            {
                logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
            }
            else
            {
                logger.LogWarning("Bad request to {Path}: {Message}", context.Request.Path, ex.Message);
            }

            context.Response.Clear();
            context.Response.StatusCode = problem.Status!.Value;
            await problemDetails.WriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem });
        }
    }
}
