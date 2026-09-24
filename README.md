# User Management API

An internal REST API for TechHive Solutions' HR and IT departments to create, retrieve, update and delete user records. Built with ASP.NET Core minimal APIs on .NET 10.

## Features

- CRUD endpoints for users, with a paged list
- Input validation (required fields, length limits, email format, no control characters) and whitespace trimming
- Unique email addresses (case-insensitive)
- Bearer-token authentication
- Request/response audit logging
- Consistent JSON error responses, including for unhandled exceptions
- OpenAPI document in Development

> Users are stored in memory, so all data is lost when the app restarts.

## Getting started

**Requirements:** [.NET 10 SDK](https://dotnet.microsoft.com/download)

```bash
dotnet run --project UserManagementAPI --launch-profile http
```

The API listens on `http://localhost:5190` (HTTPS profile: `https://localhost:7076`). In Development the OpenAPI document is at `/openapi/v1.json`.

Ready-made sample requests are in [UserManagementAPI/UserManagementAPI.http](UserManagementAPI/UserManagementAPI.http) (VS Code REST Client / Visual Studio).

## Authentication

Every endpoint except `/health` (and the Development OpenAPI document) requires a bearer token:

```
Authorization: Bearer <token>
```

Valid tokens are read from the `Authentication:Tokens` configuration array. The app refuses to start if none are configured.

| Environment | How to set the token |
|---|---|
| Development | Preconfigured in `appsettings.Development.json` as `dev-only-token-change-me` (a placeholder, never use it for real) |
| Production | Environment variable `Authentication__Tokens__0=<your-secret>` (add `__1`, `__2`, ... for more tokens) |

A missing or invalid token returns `401 Unauthorized`.

## Endpoints

| Method | Route | Description | Success | Errors |
|---|---|---|---|---|
| GET | `/health` | Health check (no token needed) | 200 | |
| GET | `/api/users?page=1&pageSize=50` | List users, ordered by ID. `pageSize` is 1-100 (default 50); the total count is in the `X-Total-Count` header | 200 | 400, 401 |
| GET | `/api/users/{id}` | Get one user | 200 | 401, 404 |
| POST | `/api/users` | Create a user | 201 | 400, 401, 409 |
| PUT | `/api/users/{id}` | Replace a user's details | 200 | 400, 401, 404, 409 |
| DELETE | `/api/users/{id}` | Delete a user | 204 | 401, 404 |

### User payload

```json
{
  "firstName": "Ada",
  "lastName": "Lovelace",
  "email": "ada.lovelace@techhive.com",
  "department": "IT"
}
```

All fields are required. Names and department are limited to 50 characters, email to 254. Responses add an `id`.

### Example

```bash
curl -X POST http://localhost:5190/api/users \
  -H "Authorization: Bearer dev-only-token-change-me" \
  -H "Content-Type: application/json" \
  -d '{"firstName":"Ada","lastName":"Lovelace","email":"ada.lovelace@techhive.com","department":"IT"}'
```

## Error responses

Every error is JSON in [problem details](https://www.rfc-editor.org/rfc/rfc9457) format and includes a short `error` message:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "User not found",
  "status": 404,
  "detail": "No user exists with ID 99.",
  "error": "No user exists with ID 99."
}
```

Unhandled exceptions return `500` with `"error": "Internal server error."`; details go to the server log only, never to the client.

## Middleware pipeline

Order matters, since each component wraps everything after it:

1. **Error handling** ([ErrorHandlingMiddleware.cs](UserManagementAPI/Middleware/ErrorHandlingMiddleware.cs)): catches unhandled exceptions and returns the standard error body.
2. **Authentication** ([TokenAuthenticationMiddleware.cs](UserManagementAPI/Middleware/TokenAuthenticationMiddleware.cs)): validates the bearer token (constant-time comparison) and returns 401 otherwise.
3. **Logging** ([RequestLoggingMiddleware.cs](UserManagementAPI/Middleware/RequestLoggingMiddleware.cs)): logs the method, path and response status of each request. Query strings, headers and bodies are never logged.

Because logging sits inside authentication, requests rejected with 401 never reach it. The authentication middleware logs those itself (`Rejected unauthenticated request: ...`).

## Project structure

```
UserManagementAPI/
├── Program.cs              Service registration and middleware pipeline
├── Endpoints/              Route definitions (/api/users)
├── Middleware/             Error handling, authentication, logging
├── Models/                 User and UserRequest (validation rules)
└── Services/               IUserRepository and the in-memory implementation
```

## Limitations

- In-memory storage only; swap in another `IUserRepository` implementation for a database.
- Static-token authentication with no expiry, users or roles. Use JWT with an identity provider for anything beyond internal tooling.
- No automated test suite yet.
