using WorkTracker.Shared;
using WorkTracker.WorkItems.Features.CreateWorkItem;
using WorkTracker.WorkItems.Infrastructure;

namespace WorkTracker.WorkItems.Tests.Features;

public class CreateWorkItemHandlerTests
{
    private static readonly Guid AssigneeId = Guid.NewGuid();
    private readonly InMemoryWorkItemRepository _repository = new();

    [Fact]
    public async Task TC_W04_Unknown_assignee_throws_BusinessRuleViolationException_and_saves_nothing()
    {
        var handler = new CreateWorkItemHandler(_repository, new StubUsersApi(userExists: false));

        var exception = await Should.ThrowAsync<BusinessRuleViolationException>(() =>
            handler.ExecuteAsync(new CreateWorkItemCommand("Write README", AssigneeId), CancellationToken.None));

        exception.Message.ShouldBe($"User '{AssigneeId}' does not exist.");
        (await _repository.GetByAssigneeAsync(AssigneeId, CancellationToken.None)).ShouldBeEmpty();
    }

    [Fact]
    public async Task TC_W05_Known_assignee_creates_and_saves_the_work_item()
    {
        var handler = new CreateWorkItemHandler(_repository, new StubUsersApi(userExists: true));

        var response = await handler.ExecuteAsync(
            new CreateWorkItemCommand("  Write README  ", AssigneeId), CancellationToken.None);

        response.Id.ShouldNotBe(Guid.Empty);
        response.Name.ShouldBe("Write README");
        response.AssigneeId.ShouldBe(AssigneeId);
        var saved = await _repository.GetByAssigneeAsync(AssigneeId, CancellationToken.None);
        saved.ShouldHaveSingleItem().Id.ShouldBe(response.Id);
    }
}
