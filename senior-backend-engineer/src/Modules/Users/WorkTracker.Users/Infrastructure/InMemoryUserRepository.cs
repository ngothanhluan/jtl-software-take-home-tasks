using System.Collections.Concurrent;
using WorkTracker.Users.Domain;

namespace WorkTracker.Users.Infrastructure;

internal sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<Guid, User> _usersById = new();

    // Keyed by Username, whose equality ignores case. TryAdd lets exactly one caller win,
    // like a unique index would in SQL.
    private readonly ConcurrentDictionary<Username, Guid> _takenUsernames = new();

    public Task<bool> TryAddAsync(User user, CancellationToken ct)
    {
        if (!_takenUsernames.TryAdd(user.Username, user.Id))
            return Task.FromResult(false);

        _usersById[user.Id] = user;
        return Task.FromResult(true);
    }

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_usersById.GetValueOrDefault(id));

    public Task<bool> ExistsAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(_usersById.ContainsKey(id));
}
