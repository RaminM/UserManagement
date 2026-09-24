using UserManagementAPI.Endpoints;
using UserManagementAPI.Middleware;
using UserManagementAPI.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Services
// ---------------------------------------------------------------------------
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options =>
    // Every error response (400/401/404/409/500, from endpoints or middleware) carries a short
    // "error" message, e.g. { "error": "Internal server error.", "status": 500, ... }.
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["error"] = context.ProblemDetails.Detail ?? context.ProblemDetails.Title);
builder.Services.AddValidation(); // enforces DataAnnotations on minimal API request bodies
builder.Services.AddSingleton<IUserRepository, InMemoryUserRepository>();

var app = builder.Build();

// ---------------------------------------------------------------------------
// Middleware pipeline (order matters: each component wraps everything below it)
// ---------------------------------------------------------------------------
// 1. Error handling: outermost, so it catches exceptions from every component below.
app.UseMiddleware<ErrorHandlingMiddleware>();

// Turns bare 404/405/415 responses into the standard JSON error body.
app.UseStatusCodePages();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseHttpsRedirection();

// 2. Authentication: rejects requests without a valid bearer token (401).
app.UseMiddleware<TokenAuthenticationMiddleware>();

// 3. Logging: innermost, so it logs the final status of every authenticated request.
//    Rejected (401) requests never reach it; the auth middleware logs those itself.
app.UseMiddleware<RequestLoggingMiddleware>();

// ---------------------------------------------------------------------------
// Endpoints
// ---------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// Left open so load balancers / uptime probes can call it without a token.
app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
   .WithName("HealthCheck")
   .AllowAnonymous();

app.MapUserEndpoints();

app.Run();
