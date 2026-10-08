namespace WorkTracker.Users.Contracts;

// The only thing other modules may ask the Users module.
public interface IUsersApi
{
    Task<bool> UserExistsAsync(Guid userId, CancellationToken ct);
}
