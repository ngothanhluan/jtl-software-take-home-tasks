using WorkTracker.Shared;

namespace WorkTracker.WorkItems.Domain;

internal sealed class WorkItem
{
    private WorkItem(Guid id, WorkItemName name, Guid assigneeId)
    {
        Id = id;
        Name = name;
        AssigneeId = assigneeId;
    }

    public Guid Id { get; }
    public WorkItemName Name { get; }

    // A user id owned by the Users module. Only the id crosses the boundary.
    public Guid AssigneeId { get; }

    public static WorkItem Create(WorkItemName name, Guid assigneeId)
    {
        if (assigneeId == Guid.Empty)
            throw new DomainException("Assignee id is required.");

        return new WorkItem(Guid.NewGuid(), name, assigneeId);
    }
}
