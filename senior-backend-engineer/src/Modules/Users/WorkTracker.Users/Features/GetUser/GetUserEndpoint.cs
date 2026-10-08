using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace WorkTracker.Users.Features.GetUser;

internal sealed class GetUserEndpoint : Endpoint<GetUserRequest, UserResponse>
{
    public override void Configure()
    {
        Get("/users/{id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetUserRequest req, CancellationToken ct)
    {
        var user = await new GetUserQuery(req.Id).ExecuteAsync(ct);

        if (user is null)
        {
            await Send.ResultAsync(TypedResults.Problem(
                detail: $"User '{req.Id}' was not found.", statusCode: StatusCodes.Status404NotFound));
            return;
        }

        await Send.OkAsync(user, ct);
    }
}
