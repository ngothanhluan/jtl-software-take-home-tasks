using WorkTracker.Users.Contracts;

namespace WorkTracker.WorkItems.Tests.Features;

// Stands in for the Users module: always gives the same answer.
internal sealed class StubUsersApi(bool userExists) : IUsersApi
{
    public Task<bool> UserExistsAsync(Guid userId, CancellationToken ct) => Task.FromResult(userExists);
}
