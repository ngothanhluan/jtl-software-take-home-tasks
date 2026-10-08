using WorkTracker.WorkItems.Domain;

namespace WorkTracker.WorkItems.Features;

internal sealed record WorkItemResponse(Guid Id, string Name, Guid AssigneeId)
{
    public static WorkItemResponse From(WorkItem workItem) =>
        new(workItem.Id, workItem.Name.Value, workItem.AssigneeId);
}
