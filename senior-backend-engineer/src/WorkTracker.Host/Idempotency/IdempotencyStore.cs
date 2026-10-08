using System.Collections.Concurrent;

namespace WorkTracker.Host.Idempotency;

internal sealed record StoredResponse(int StatusCode, string? Location, string Body);

internal enum ReservationStatus { Reserved, InProgress, Completed, KeyReusedForDifferentRequest }

internal sealed record Reservation(ReservationStatus Status, StoredResponse? Response = null);

// Remembers each Idempotency-Key for a time window, in process memory.
// In production this would be Redis SET key NX EX <window>, shared by every instance.
internal sealed class IdempotencyStore(TimeProvider time, TimeSpan window)
{
    private sealed record Entry(string Fingerprint, DateTimeOffset ExpiresAt, StoredResponse? Response);

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public int Count => _entries.Count;

    public Reservation Reserve(string key, string fingerprint)
    {
        var now = time.GetUtcNow();
        RemoveExpired(now);

        var reserved = new Entry(fingerprint, now + window, Response: null);
        var entry = _entries.GetOrAdd(key, reserved);   // atomic: exactly one caller adds

        if (ReferenceEquals(entry, reserved))
            return new Reservation(ReservationStatus.Reserved);
        if (entry.Fingerprint != fingerprint)
            return new Reservation(ReservationStatus.KeyReusedForDifferentRequest);
        return entry.Response is null
            ? new Reservation(ReservationStatus.InProgress)
            : new Reservation(ReservationStatus.Completed, entry.Response);
    }

    // Stores the successful response; the window restarts from now.
    public void Complete(string key, StoredResponse response)
    {
        if (_entries.TryGetValue(key, out var entry))
            _entries.TryUpdate(key, entry with { Response = response, ExpiresAt = time.GetUtcNow() + window }, entry);
    }

    // Forgets the key after a failed request, so the client can retry with it.
    public void Release(string key) => _entries.TryRemove(key, out _);

    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var entry in _entries)
        {
            if (entry.Value.ExpiresAt <= now)
                _entries.TryRemove(entry);
        }
    }
}
