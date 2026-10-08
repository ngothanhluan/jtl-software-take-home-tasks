using FastEndpoints;

namespace WorkTracker.WorkItems.Features.ListWorkItemsByAssignee;

internal sealed class ListWorkItemsByAssigneeEndpoint
    : Endpoint<ListWorkItemsByAssigneeRequest, IReadOnlyList<WorkItemResponse>>
{
    public override void Configure()
    {
        Get("/work-items");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ListWorkItemsByAssigneeRequest req, CancellationToken ct)
    {
        var workItems = await new ListWorkItemsByAssigneeQuery(req.AssigneeId).ExecuteAsync(ct);
        await Send.OkAsync(workItems, ct);
    }
}
