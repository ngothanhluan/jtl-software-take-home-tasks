using WorkTracker.Users.Contracts;
using WorkTracker.Users.Domain;

namespace WorkTracker.Users;

internal sealed class UsersApi(IUserRepository users) : IUsersApi
{
    public Task<bool> UserExistsAsync(Guid userId, CancellationToken ct) => users.ExistsAsync(userId, ct);
}
