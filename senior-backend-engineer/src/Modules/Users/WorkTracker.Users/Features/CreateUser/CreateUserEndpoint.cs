using FastEndpoints;
using WorkTracker.Users.Features.GetUser;

namespace WorkTracker.Users.Features.CreateUser;

internal sealed class CreateUserEndpoint : Endpoint<CreateUserRequest, UserResponse>
{
    public override void Configure()
    {
        Post("/users");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
    {
        var user = await new CreateUserCommand(req.Username).ExecuteAsync(ct);
        await Send.CreatedAtAsync<GetUserEndpoint>(new { id = user.Id }, user, cancellation: ct);
    }
}
