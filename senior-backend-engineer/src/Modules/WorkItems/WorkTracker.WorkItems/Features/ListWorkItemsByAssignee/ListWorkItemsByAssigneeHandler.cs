using FastEndpoints;
using WorkTracker.Shared;
using WorkTracker.WorkItems.Domain;

namespace WorkTracker.WorkItems.Features.ListWorkItemsByAssignee;

// Read only. An unknown assignee simply has no work items; reads never call another module.
internal sealed class ListWorkItemsByAssigneeHandler(IWorkItemRepository workItems)
    : ICommandHandler<ListWorkItemsByAssigneeQuery, IReadOnlyList<WorkItemResponse>>
{
    public async Task<IReadOnlyList<WorkItemResponse>> ExecuteAsync(ListWorkItemsByAssigneeQuery query, CancellationToken ct)
    {
        if (query.AssigneeId == Guid.Empty)
            throw new DomainException("Assignee id is required.");

        var items = await workItems.GetByAssigneeAsync(query.AssigneeId, ct);
        return items.Select(WorkItemResponse.From).ToList();
    }
}
