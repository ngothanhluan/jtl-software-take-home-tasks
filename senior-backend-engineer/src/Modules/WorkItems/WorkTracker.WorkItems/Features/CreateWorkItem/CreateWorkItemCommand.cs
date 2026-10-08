using FastEndpoints;

namespace WorkTracker.WorkItems.Features.CreateWorkItem;

internal sealed record CreateWorkItemCommand(string? Name, Guid AssigneeId) : ICommand<WorkItemResponse>;
