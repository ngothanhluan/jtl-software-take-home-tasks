using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace WorkTracker.Api.Tests;

internal sealed record UserDto(Guid Id, string Username);
internal sealed record WorkItemDto(Guid Id, string Name, Guid AssigneeId);

internal static class TestSupport
{
    public const string KeyHeader = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotent-Replayed";

    // A unique, valid username so tests never collide.
    public static string NewUsername() => "user" + Guid.NewGuid().ToString("N")[..8];

    // POSTs JSON with an Idempotency-Key: a fresh one unless the test passes its own.
    public static Task<HttpResponseMessage> PostWithKeyAsync(
        this HttpClient client, string url, object body, string? key = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
        request.Headers.Add(KeyHeader, key ?? Guid.NewGuid().ToString());
        return client.SendAsync(request);
    }

    public static async Task<UserDto> CreateUserAsync(this HttpClient client)
    {
        var response = await client.PostWithKeyAsync("/users", new { username = NewUsername() });
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<UserDto>())!;
    }

    public static async Task<ProblemDetails> ShouldBeProblemAsync(
        this HttpResponseMessage response, HttpStatusCode expectedStatus)
    {
        response.StatusCode.ShouldBe(expectedStatus);
        response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/problem+json");
        return (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;
    }
}
