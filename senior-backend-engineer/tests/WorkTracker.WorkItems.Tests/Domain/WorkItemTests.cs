using WorkTracker.Shared;
using WorkTracker.WorkItems.Domain;

namespace WorkTracker.WorkItems.Tests.Domain;

public class WorkItemTests
{
    [Fact]
    public void TC_W03_Empty_assignee_throws_DomainException()
    {
        var exception = Should.Throw<DomainException>(
            () => WorkItem.Create(new WorkItemName("Write README"), Guid.Empty));

        exception.Message.ShouldBe("Assignee id is required.");
    }
}
