namespace WorkTracker.WorkItems.Domain;

internal interface IWorkItemRepository
{
    Task AddAsync(WorkItem workItem, CancellationToken ct);
    Task<IReadOnlyList<WorkItem>> GetByAssigneeAsync(Guid assigneeId, CancellationToken ct);
}
