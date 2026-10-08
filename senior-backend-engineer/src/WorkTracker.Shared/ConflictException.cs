namespace WorkTracker.Shared;

// The thing being created already exists (e.g. a taken username). Maps to 409.
public sealed class ConflictException(string message) : Exception(message);
