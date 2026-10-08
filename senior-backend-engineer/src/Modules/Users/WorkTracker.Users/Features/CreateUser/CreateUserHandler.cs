using FastEndpoints;
using WorkTracker.Shared;
using WorkTracker.Users.Domain;

namespace WorkTracker.Users.Features.CreateUser;

internal sealed class CreateUserHandler(IUserRepository users) : ICommandHandler<CreateUserCommand, UserResponse>
{
    public async Task<UserResponse> ExecuteAsync(CreateUserCommand command, CancellationToken ct)
    {
        var user = User.Create(new Username(command.Username));

        if (!await users.TryAddAsync(user, ct))
            throw new ConflictException($"Username '{user.Username.Value}' already exists.");

        return UserResponse.From(user);
    }
}
