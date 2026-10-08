using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using static WorkTracker.Api.Tests.TestSupport;

namespace WorkTracker.Api.Tests.Idempotency;

public class IdempotencyEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string KeyUsedForDifferentRequest = "Idempotency-Key was already used with a different request.";
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task TC_I01_User_post_without_a_key_returns_400_and_creates_nothing()
    {
        var username = NewUsername();

        var problem = await (await _client.PostAsJsonAsync("/users", new { username })).ShouldBeProblemAsync(HttpStatusCode.BadRequest);

        problem.Detail.ShouldBe("Idempotency-Key header is required.");
        (await _client.PostWithKeyAsync("/users", new { username })).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task TC_I02_Replayed_work_item_post_returns_the_stored_response_and_creates_nothing()
    {
        var user = await _client.CreateUserAsync();
        var key = Guid.NewGuid().ToString();
        var body = new { name = "Write README", assigneeId = user.Id };

        var first = await _client.PostWithKeyAsync("/work-items", body, key);
        var second = await _client.PostWithKeyAsync("/work-items", body, key);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        first.Headers.Contains(ReplayedHeader).ShouldBeFalse();
        second.StatusCode.ShouldBe(HttpStatusCode.Created);
        second.Headers.GetValues(ReplayedHeader).ShouldBe(new[] { "true" });
        (await second.Content.ReadFromJsonAsync<WorkItemDto>())
            .ShouldBe(await first.Content.ReadFromJsonAsync<WorkItemDto>());
        var items = await _client.GetFromJsonAsync<WorkItemDto[]>($"/work-items?assigneeId={user.Id}");
        items.ShouldNotBeNull().Length.ShouldBe(1);
    }

    [Fact]
    public async Task TC_I03_Key_reused_with_a_different_body_or_endpoint_returns_422()
    {
        var key = Guid.NewGuid().ToString();
        (await _client.PostWithKeyAsync("/users", new { username = NewUsername() }, key))
            .StatusCode.ShouldBe(HttpStatusCode.Created);

        var differentBody = await (await _client.PostWithKeyAsync("/users", new { username = NewUsername() }, key))
            .ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity);
        var differentEndpoint = await (await _client.PostWithKeyAsync("/work-items", new { name = "x", assigneeId = Guid.NewGuid() }, key))
            .ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity);

        differentBody.Detail.ShouldBe(KeyUsedForDifferentRequest);
        differentEndpoint.Detail.ShouldBe(KeyUsedForDifferentRequest);
    }

    [Fact]
    public async Task TC_I04_A_failed_request_releases_its_key()
    {
        var key = Guid.NewGuid().ToString();

        (await _client.PostWithKeyAsync("/users", new { username = "a" }, key)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await _client.PostWithKeyAsync("/users", new { username = NewUsername() }, key)).StatusCode.ShouldBe(HttpStatusCode.Created);
    }
}
