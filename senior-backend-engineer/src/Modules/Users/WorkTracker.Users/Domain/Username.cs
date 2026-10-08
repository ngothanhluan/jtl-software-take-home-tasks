using System.Text.RegularExpressions;
using WorkTracker.Shared;

namespace WorkTracker.Users.Domain;

// 3-32 ASCII letters, digits, '.', '_' or '-', trimmed. Two usernames are equal ignoring case.
internal sealed partial record Username
{
    private const int MinLength = 3;
    private const int MaxLength = 32;

    public Username(string? value)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0)
            throw new DomainException("Username is required.");
        if (trimmed.Length is < MinLength or > MaxLength)
            throw new DomainException($"Username must be between {MinLength} and {MaxLength} characters.");
        if (!AllowedCharacters().IsMatch(trimmed))
            throw new DomainException("Username can only contain letters, digits, '.', '_' and '-'.");

        Value = trimmed;
    }

    public string Value { get; }

    public bool Equals(Username? other) =>
        other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

    public override string ToString() => Value;

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex AllowedCharacters();
}
