using WorkTracker.Users.Domain;

namespace WorkTracker.Users.Features;

internal sealed record UserResponse(Guid Id, string Username)
{
    public static UserResponse From(User user) => new(user.Id, user.Username.Value);
}
