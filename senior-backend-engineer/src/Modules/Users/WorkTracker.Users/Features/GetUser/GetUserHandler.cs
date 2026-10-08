using FastEndpoints;
using WorkTracker.Users.Domain;

namespace WorkTracker.Users.Features.GetUser;

// Read only: returns null when the user does not exist; the endpoint turns that into 404.
internal sealed class GetUserHandler(IUserRepository users) : ICommandHandler<GetUserQuery, UserResponse?>
{
    public async Task<UserResponse?> ExecuteAsync(GetUserQuery query, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(query.Id, ct);
        return user is null ? null : UserResponse.From(user);
    }
}
