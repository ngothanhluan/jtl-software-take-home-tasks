namespace WorkTracker.Shared;

// A rule of a single object is broken (e.g. a username that is too short). Maps to 400.
public sealed class DomainException(string message) : Exception(message);
