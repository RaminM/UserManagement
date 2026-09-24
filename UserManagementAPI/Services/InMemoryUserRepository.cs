using UserManagementAPI.Models;

namespace UserManagementAPI.Services;

/// <summary>Thread-safe in-memory store. Data is lost when the app restarts.</summary>
public class InMemoryUserRepository : IUserRepository
{
    // Ordered by ID, so listing needs no sort and paging needs no full copy.
    private readonly SortedDictionary<int, User> _users = new();

    // Case-insensitive email -> user ID, so uniqueness checks are O(1) instead of a full scan.
    private readonly Dictionary<string, int> _emailIndex = new(StringComparer.OrdinalIgnoreCase);

    private readonly ReaderWriterLockSlim _lock = new();
    private int _nextId = 1;

    public (IReadOnlyList<User> Items, int Total) GetPage(int page, int pageSize)
    {
        _lock.EnterReadLock();
        try
        {
            long skip = (long)(page - 1) * pageSize;
            if (skip >= _users.Count)
            {
                return ([], _users.Count);
            }

            var items = _users.Values.Skip((int)skip).Take(pageSize).ToList();
            return (items, _users.Count);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public User? GetById(int id)
    {
        _lock.EnterReadLock();
        try
        {
            return _users.GetValueOrDefault(id);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public User? Add(UserRequest request)
    {
        _lock.EnterWriteLock();
        try
        {
            if (_emailIndex.ContainsKey(request.Email))
            {
                return null;
            }

            var user = new User(_nextId++, request.FirstName, request.LastName, request.Email, request.Department);
            _users[user.Id] = user;
            _emailIndex[user.Email] = user.Id;
            return user;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public UpdateResult Update(int id, UserRequest request, out User? updated)
    {
        _lock.EnterWriteLock();
        try
        {
            updated = null;

            if (!_users.TryGetValue(id, out var existing))
            {
                return UpdateResult.NotFound;
            }

            if (_emailIndex.TryGetValue(request.Email, out var ownerId) && ownerId != id)
            {
                return UpdateResult.EmailConflict;
            }

            _emailIndex.Remove(existing.Email);
            updated = new User(id, request.FirstName, request.LastName, request.Email, request.Department);
            _users[id] = updated;
            _emailIndex[updated.Email] = id;
            return UpdateResult.Updated;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool Delete(int id)
    {
        _lock.EnterWriteLock();
        try
        {
            if (!_users.Remove(id, out var removed))
            {
                return false;
            }

            _emailIndex.Remove(removed.Email);
            return true;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }
}
