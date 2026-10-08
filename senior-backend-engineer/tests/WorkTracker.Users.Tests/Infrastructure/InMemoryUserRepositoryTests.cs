using WorkTracker.Users.Domain;
using WorkTracker.Users.Infrastructure;

namespace WorkTracker.Users.Tests.Infrastructure;

public class InMemoryUserRepositoryTests
{
    [Fact]
    public async Task TC_U05_Concurrent_adds_of_the_same_username_let_exactly_one_win()
    {
        var repository = new InMemoryUserRepository();
        string[] spellings = ["alice", "Alice", "ALICE", "aLiCe"];

        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Task.Run(() =>
            repository.TryAddAsync(User.Create(new Username(spellings[i % 4])), CancellationToken.None))));

        results.Count(added => added).ShouldBe(1);
    }
}
