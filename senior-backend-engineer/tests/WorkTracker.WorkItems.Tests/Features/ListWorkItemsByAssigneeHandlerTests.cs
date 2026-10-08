using WorkTracker.WorkItems.Domain;
using WorkTracker.WorkItems.Features.ListWorkItemsByAssignee;
using WorkTracker.WorkItems.Infrastructure;

namespace WorkTracker.WorkItems.Tests.Features;

public class ListWorkItemsByAssigneeHandlerTests
{
    [Fact]
    public async Task TC_W06_Lists_only_the_given_assignees_work_items()
    {
        var repository = new InMemoryWorkItemRepository();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await repository.AddAsync(WorkItem.Create(new WorkItemName("A1"), userA), CancellationToken.None);
        await repository.AddAsync(WorkItem.Create(new WorkItemName("A2"), userA), CancellationToken.None);
        await repository.AddAsync(WorkItem.Create(new WorkItemName("B1"), userB), CancellationToken.None);
        var handler = new ListWorkItemsByAssigneeHandler(repository);

        var forA = await handler.ExecuteAsync(new ListWorkItemsByAssigneeQuery(userA), CancellationToken.None);
        var forUnknown = await handler.ExecuteAsync(new ListWorkItemsByAssigneeQuery(Guid.NewGuid()), CancellationToken.None);

        forA.Select(item => item.Name).ShouldBe(new[] { "A1", "A2" }, ignoreOrder: true);
        forUnknown.ShouldBeEmpty();
    }
}
