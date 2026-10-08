namespace WorkTracker.WorkItems.Features.ListWorkItemsByAssignee;

internal sealed class ListWorkItemsByAssigneeRequest
{
    // Bound from ?assigneeId=. A value that is not a GUID fails binding with a 400.
    public Guid AssigneeId { get; set; }
}
