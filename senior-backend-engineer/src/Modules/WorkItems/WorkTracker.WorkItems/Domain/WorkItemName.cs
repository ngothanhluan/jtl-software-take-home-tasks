using WorkTracker.Shared;

namespace WorkTracker.WorkItems.Domain;

// 1-200 characters, trimmed.
internal sealed record WorkItemName
{
    private const int MaxLength = 200;

    public WorkItemName(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
            throw new DomainException("Work item name is required.");
        if (trimmed.Length > MaxLength)
            throw new DomainException($"Work item name must be at most {MaxLength} characters.");

        Value = trimmed;
    }

    public string Value { get; }

    public override string ToString() => Value;
}
