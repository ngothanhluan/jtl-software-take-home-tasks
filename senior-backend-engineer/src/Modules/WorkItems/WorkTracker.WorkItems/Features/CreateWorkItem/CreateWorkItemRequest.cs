namespace WorkTracker.WorkItems.Features.CreateWorkItem;

internal sealed class CreateWorkItemRequest
{
    public string? Name { get; set; }
    public Guid AssigneeId { get; set; }
}
