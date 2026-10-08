using FastEndpoints;

namespace WorkTracker.Users.Features.GetUser;

internal sealed record GetUserQuery(Guid Id) : ICommand<UserResponse?>;
