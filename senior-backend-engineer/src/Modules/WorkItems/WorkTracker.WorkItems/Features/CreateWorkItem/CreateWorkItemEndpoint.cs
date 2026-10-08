using FastEndpoints;
using Microsoft.AspNetCore.Http;

namespace WorkTracker.WorkItems.Features.CreateWorkItem;

// 201 without a Location header: there is no get-one-work-item endpoint to point at.
internal sealed class CreateWorkItemEndpoint : Endpoint<CreateWorkItemRequest, WorkItemResponse>
{
    public override void Configure()
    {
        Post("/work-items");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateWorkItemRequest req, CancellationToken ct)
    {
        var workItem = await new CreateWorkItemCommand(req.Name, req.AssigneeId).ExecuteAsync(ct);
        await Send.ResponseAsync(workItem, StatusCodes.Status201Created, ct);
    }
}
