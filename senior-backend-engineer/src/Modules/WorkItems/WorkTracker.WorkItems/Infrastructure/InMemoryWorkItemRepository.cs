using System.Collections.Concurrent;
using WorkTracker.WorkItems.Domain;

namespace WorkTracker.WorkItems.Infrastructure;

internal sealed class InMemoryWorkItemRepository : IWorkItemRepository
{
    private readonly ConcurrentDictionary<Guid, WorkItem> _workItems = new();

    public Task AddAsync(WorkItem workItem, CancellationToken ct)
    {
        _workItems[workItem.Id] = workItem;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<WorkItem>> GetByAssigneeAsync(Guid assigneeId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<WorkItem>>(
            _workItems.Values.Where(item => item.AssigneeId == assigneeId).ToList());
}
