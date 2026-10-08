using FastEndpoints;

namespace WorkTracker.WorkItems.Features.ListWorkItemsByAssignee;

internal sealed record ListWorkItemsByAssigneeQuery(Guid AssigneeId) : ICommand<IReadOnlyList<WorkItemResponse>>;
