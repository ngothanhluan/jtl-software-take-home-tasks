using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using static WorkTracker.Api.Tests.TestSupport;

namespace WorkTracker.Api.Tests;

public class UsersEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task TC_A01_Create_user_returns_201_with_body_and_location()
    {
        var username = NewUsername();

        var response = await _client.PostWithKeyAsync("/users", new { username });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var user = (await response.Content.ReadFromJsonAsync<UserDto>())!;
        user.Id.ShouldNotBe(Guid.Empty);
        user.Username.ShouldBe(username);
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"/users/{user.Id}");
    }

    [Fact]
    public async Task TC_A02_Get_user_at_location_returns_200_with_the_same_user()
    {
        var created = await _client.PostWithKeyAsync("/users", new { username = NewUsername() });

        var response = await _client.GetAsync(created.Headers.Location);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<UserDto>())
            .ShouldBe(await created.Content.ReadFromJsonAsync<UserDto>());
    }

    [Fact]
    public async Task TC_A03_Get_unknown_user_returns_404_problem()
    {
        var id = Guid.NewGuid();

        var problem = await (await _client.GetAsync($"/users/{id}")).ShouldBeProblemAsync(HttpStatusCode.NotFound);

        problem.Detail.ShouldBe($"User '{id}' was not found.");
    }

    [Fact]
    public async Task TC_A04_Invalid_username_returns_400_problem()
    {
        var response = await _client.PostWithKeyAsync("/users", new { username = "a" });

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest);
        problem.Detail.ShouldBe("Username must be between 3 and 32 characters.");
    }

    [Fact]
    public async Task TC_A04_Invalid_username_returns_problem_body_even_when_client_does_not_ask_for_json()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/users") { Content = JsonContent.Create(new { username = "a" }) };
        request.Headers.Add(KeyHeader, Guid.NewGuid().ToString());
        request.Headers.Add("Accept", "text/html");

        var problem = await (await _client.SendAsync(request)).ShouldBeProblemAsync(HttpStatusCode.BadRequest);

        problem.Detail.ShouldBe("Username must be between 3 and 32 characters.");
    }

    [Fact]
    public async Task TC_A04_Malformed_json_returns_400_problem_not_500()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/users")
        {
            Content = new StringContent("{ not json", Encoding.UTF8, "application/json")
        };
        request.Headers.Add(KeyHeader, Guid.NewGuid().ToString());

        var problem = await (await _client.SendAsync(request)).ShouldBeProblemAsync(HttpStatusCode.BadRequest);

        problem.Detail.ShouldBe("The request body is not valid JSON.");
        problem.Extensions.ShouldNotContainKey("errors");
    }

    [Fact]
    public async Task TC_A04_Wrong_json_type_returns_400_naming_the_field()
    {
        var response = await _client.PostWithKeyAsync("/users", new { username = 5 });

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest);
        problem.Detail.ShouldBe("'username' has an invalid value.");
    }

    [Fact]
    public async Task TC_A03_Get_user_with_invalid_id_returns_400_naming_the_field()
    {
        var problem = await (await _client.GetAsync("/users/not-a-guid")).ShouldBeProblemAsync(HttpStatusCode.BadRequest);

        problem.Detail.ShouldBe("'id' has an invalid value.");
    }

    [Fact]
    public async Task TC_A05_Duplicate_username_ignoring_case_returns_409_problem()
    {
        var username = NewUsername();
        await _client.PostWithKeyAsync("/users", new { username });

        var response = await _client.PostWithKeyAsync("/users", new { username = username.ToUpperInvariant() });

        var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Conflict);
        problem.Detail.ShouldBe($"Username '{username.ToUpperInvariant()}' already exists.");
    }
}
