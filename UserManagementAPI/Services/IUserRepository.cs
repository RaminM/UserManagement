using UserManagementAPI.Models;

namespace UserManagementAPI.Services;

public interface IUserRepository
{
    /// <summary>Returns one page of users ordered by ID, plus the total user count.</summary>
    (IReadOnlyList<User> Items, int Total) GetPage(int page, int pageSize);

    User? GetById(int id);

    /// <summary>Adds a user. Returns null if the email is already in use.</summary>
    User? Add(UserRequest request);

    /// <summary>Updates a user. Returns <see cref="UpdateResult"/> describing the outcome.</summary>
    UpdateResult Update(int id, UserRequest request, out User? updated);

    bool Delete(int id);
}

public enum UpdateResult
{
    Updated,
    NotFound,
    EmailConflict
}
