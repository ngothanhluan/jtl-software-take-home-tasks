namespace WorkTracker.Host.Idempotency;

// Shared by the pre- and post-processor of one request (FastEndpoints ProcessorState).
internal sealed class IdempotencyState
{
    // Set only when this request reserved the key, so only the owner completes or releases it.
    public string? ReservedKey { get; set; }
}
