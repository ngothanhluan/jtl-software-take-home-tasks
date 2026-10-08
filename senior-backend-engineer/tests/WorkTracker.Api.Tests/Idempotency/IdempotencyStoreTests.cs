using Microsoft.Extensions.Time.Testing;
using WorkTracker.Host.Idempotency;

namespace WorkTracker.Api.Tests.Idempotency;

public class IdempotencyStoreTests
{
    private readonly FakeTimeProvider _time = new();
    private readonly IdempotencyStore _store;

    public IdempotencyStoreTests() => _store = new IdempotencyStore(_time, TimeSpan.FromSeconds(10));

    [Fact]
    public async Task TC_I05_Concurrent_reservations_of_one_key_let_exactly_one_through()
    {
        var reservations = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => _store.Reserve("K", "fingerprint"))));

        reservations.Count(r => r.Status == ReservationStatus.Reserved).ShouldBe(1);
        reservations.Count(r => r.Status == ReservationStatus.InProgress).ShouldBe(19);
    }

    [Fact]
    public void TC_I06_Completed_key_replays_inside_the_window_and_is_forgotten_after_it()
    {
        var stored = new StoredResponse(201, "/users/1", "{}");
        _store.Reserve("K", "fingerprint");
        _store.Complete("K", stored);
        _store.Reserve("other", "fingerprint");
        _store.Complete("other", stored);

        _time.Advance(TimeSpan.FromSeconds(9));
        _store.Reserve("K", "fingerprint").ShouldBe(new Reservation(ReservationStatus.Completed, stored));

        _time.Advance(TimeSpan.FromSeconds(2));
        _store.Reserve("K", "fingerprint").Status.ShouldBe(ReservationStatus.Reserved);
        _store.Count.ShouldBe(1); // "other" expired and was purged
    }
}
