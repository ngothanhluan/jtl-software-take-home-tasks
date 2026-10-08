namespace WorkTracker.Users.Domain;

internal interface IUserRepository
{
    // Atomic: returns false when the username is already taken (ignoring case).
    Task<bool> TryAddAsync(User user, CancellationToken ct);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<bool> ExistsAsync(Guid id, CancellationToken ct);
}
