using FastEndpoints;

namespace WorkTracker.Users.Features.CreateUser;

internal sealed record CreateUserCommand(string? Username) : ICommand<UserResponse>;
