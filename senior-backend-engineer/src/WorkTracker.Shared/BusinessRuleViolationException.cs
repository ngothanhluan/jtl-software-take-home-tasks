namespace WorkTracker.Shared;

// A rule that needs other data fails (e.g. the assignee does not exist). Maps to 422.
public sealed class BusinessRuleViolationException(string message) : Exception(message);
