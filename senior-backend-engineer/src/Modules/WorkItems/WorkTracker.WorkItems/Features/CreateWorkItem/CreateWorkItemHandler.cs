using FastEndpoints;
using WorkTracker.Shared;
using WorkTracker.Users.Contracts;
using WorkTracker.WorkItems.Domain;

namespace WorkTracker.WorkItems.Features.CreateWorkItem;

internal sealed class CreateWorkItemHandler(IWorkItemRepository workItems, IUsersApi users)
    : ICommandHandler<CreateWorkItemCommand, WorkItemResponse>
{
    public async Task<WorkItemResponse> ExecuteAsync(CreateWorkItemCommand command, CancellationToken ct)
    {
        // Single-object rules first (400), then the rule that needs another module (422).
        var workItem = WorkItem.Create(new WorkItemName(command.Name), command.AssigneeId);

        if (!await users.UserExistsAsync(command.AssigneeId, ct))
            throw new BusinessRuleViolationException($"User '{command.AssigneeId}' does not exist.");

        await workItems.AddAsync(workItem, ct);
        return WorkItemResponse.From(workItem);
    }
}
