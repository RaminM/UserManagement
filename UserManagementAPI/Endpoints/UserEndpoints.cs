using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using UserManagementAPI.Models;
using UserManagementAPI.Services;

namespace UserManagementAPI.Endpoints;

public static class UserEndpoints
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;

    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/users").WithTags("Users");

        // Paged so a large directory is never serialized in one response. The body stays a plain
        // array; the total is in the X-Total-Count header.
        group.MapGet("/", (
                HttpResponse response,
                IUserRepository repo,
                [Range(1, int.MaxValue)] int page = 1,
                [Range(1, MaxPageSize)] int pageSize = DefaultPageSize) =>
            {
                var (items, total) = repo.GetPage(page, pageSize);
                response.Headers["X-Total-Count"] = total.ToString();
                return TypedResults.Ok(items);
            })
            .WithName("GetUsers")
            .ProducesValidationProblem();

        group.MapGet("/{id:int}", (int id, IUserRepository repo) =>
                repo.GetById(id) is { } user
                    ? Results.Ok(user)
                    : UserNotFound(id))
            .WithName("GetUserById")
            .Produces<User>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", (UserRequest request, IUserRepository repo) =>
            {
                var user = repo.Add(request.Normalized());
                return user is null
                    ? EmailConflict()
                    : Results.CreatedAtRoute("GetUserById", new { id = user.Id }, user);
            })
            .WithName("CreateUser")
            .Produces<User>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:int}", (int id, UserRequest request, IUserRepository repo) =>
                repo.Update(id, request.Normalized(), out var updated) switch
                {
                    UpdateResult.Updated => Results.Ok(updated),
                    UpdateResult.EmailConflict => EmailConflict(),
                    _ => UserNotFound(id)
                })
            .WithName("UpdateUser")
            .Produces<User>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:int}", (int id, IUserRepository repo) =>
                repo.Delete(id) ? Results.NoContent() : UserNotFound(id))
            .WithName("DeleteUser")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return group;
    }

    private static IResult UserNotFound(int id) => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "User not found",
        detail: $"No user exists with ID {id}.");

    private static IResult EmailConflict() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Email already in use",
        detail: "Another user already has this email address.");
}
