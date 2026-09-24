using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace UserManagementAPI.Middleware;

/// <summary>
/// Requires "Authorization: Bearer &lt;token&gt;" on every request whose endpoint is not marked
/// AllowAnonymous. Valid tokens come from the "Authentication:Tokens" configuration array.
/// </summary>
public class TokenAuthenticationMiddleware
{
    private const string Scheme = "Bearer";

    private readonly RequestDelegate _next;
    private readonly IProblemDetailsService _problemDetails;
    private readonly ILogger<TokenAuthenticationMiddleware> _logger;
    private readonly byte[][] _validTokenHashes;

    public TokenAuthenticationMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        IProblemDetailsService problemDetails,
        ILogger<TokenAuthenticationMiddleware> logger)
    {
        _next = next;
        _problemDetails = problemDetails;
        _logger = logger;

        var tokens = configuration.GetSection("Authentication:Tokens").Get<string[]>()?
            .Where(t => !string.IsNullOrWhiteSpace(t)).ToArray() ?? [];

        // Fail at startup rather than run with the API silently open or permanently locked.
        if (tokens.Length == 0)
        {
            throw new InvalidOperationException(
                "No API tokens configured. Set 'Authentication:Tokens' (e.g. environment variable " +
                "Authentication__Tokens__0) before starting the API.");
        }

        // Compare hashes in constant time so response timing doesn't leak token contents.
        _validTokenHashes = tokens.Select(Hash).ToArray();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await _next(context);
            return;
        }

        var failure = Validate(context.Request.Headers.Authorization.ToString());
        if (failure is null)
        {
            await _next(context);
            return;
        }

        // This short-circuits before the logging middleware, so record the rejection here.
        _logger.LogWarning("Rejected unauthenticated request: {Method} {Path} ({Reason})",
            context.Request.Method, LogSanitizer.Clean(context.Request.Path), failure);

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers[HeaderNames.WWWAuthenticate] = Scheme;
        await _problemDetails.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized.",
                Detail = "A valid bearer token is required."
            }
        });
    }

    /// <summary>Returns null when the header carries a valid token, otherwise the reason it was rejected.</summary>
    private string? Validate(string authorization)
    {
        if (string.IsNullOrWhiteSpace(authorization))
        {
            return "missing Authorization header";
        }

        if (authorization.Trim().Equals(Scheme, StringComparison.OrdinalIgnoreCase))
        {
            return "empty token";
        }

        if (!authorization.StartsWith(Scheme + " ", StringComparison.OrdinalIgnoreCase))
        {
            return "unsupported authorization scheme";
        }

        var candidate = Hash(authorization[(Scheme.Length + 1)..].Trim());
        var valid = false;
        foreach (var hash in _validTokenHashes)
        {
            valid |= CryptographicOperations.FixedTimeEquals(candidate, hash);
        }

        return valid ? null : "invalid token";
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
