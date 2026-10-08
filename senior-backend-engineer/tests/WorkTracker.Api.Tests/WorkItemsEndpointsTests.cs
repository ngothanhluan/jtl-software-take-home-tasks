using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using static WorkTracker.Api.Tests.TestSupport;

namespace WorkTracker.Api.Tests;

public class WorkItemsEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task TC_A06_Create_work_item_returns_201_with_body_and_no_location()
    {
        var user = await _client.CreateUserAsync();

        var response = await _client.PostWithKeyAsync("/work-items", new { name = "Write README", assigneeId = user.Id });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldBeNull();
        var item = (await response.Content.ReadFromJsonAsync<WorkItemDto>())!;
        item.Id.ShouldNotBe(Guid.Empty);
        item.Name.ShouldBe("Write README");
        item.AssigneeId.ShouldBe(user.Id);
    }

    [Fact]
    public async Task TC_A07_Unknown_assignee_returns_422_problem()
    {
        var assigneeId = Guid.NewGuid();

        var response = await _client.PostWithKeyAsync("/work-items", new { name = "Write README", assigneeId });

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity);
        problem.Detail.ShouldBe($"User '{assigneeId}' does not exist.");
    }

    [Fact]
    public async Task TC_A08_List_returns_only_the_assignees_work_items()
    {
        var userA = await _client.CreateUserAsync();
        var userB = await _client.CreateUserAsync();
        await _client.CreateWorkItemAsync(userA.Id, "A1");
        await _client.CreateWorkItemAsync(userA.Id, "A2");
        await _client.CreateWorkItemAsync(userB.Id, "B1");

        var forA = await _client.GetFromJsonAsync<WorkItemDto[]>($"/work-items?assigneeId={userA.Id}");
        var forUnknown = await _client.GetFromJsonAsync<WorkItemDto[]>($"/work-items?assigneeId={Guid.NewGuid()}");

        forA.ShouldNotBeNull().Select(item => item.Name).ShouldBe(new[] { "A1", "A2" }, ignoreOrder: true);
        forUnknown.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Theory]
    [InlineData("/work-items")]
    [InlineData("/work-items?assigneeId=not-a-guid")]
    [InlineData("/work-items?assigneeId=00000000-0000-0000-0000-000000000000")]
    public async Task TC_A09_List_without_a_valid_assignee_id_returns_400_problem(string url)
    {
        await (await _client.GetAsync(url)).ShouldBeProblemAsync(HttpStatusCode.BadRequest);
    }
}
