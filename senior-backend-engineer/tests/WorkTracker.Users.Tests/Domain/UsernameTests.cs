using WorkTracker.Shared;
using WorkTracker.Users.Domain;

namespace WorkTracker.Users.Tests.Domain;

public class UsernameTests
{
    [Theory]
    [InlineData("  alice  ", "alice")]
    public void TC_U01_Valid_username_is_accepted_and_trimmed(string input, string expected)
    {
        var username = new Username(input);

        username.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("   ", "Username is required.")]
    [InlineData("ab", "Username must be between 3 and 32 characters.")]
    [InlineData("al!ce", "Username can only contain letters, digits, '.', '_' and '-'.")]
    public void TC_U02_Invalid_username_throws_DomainException_with_specific_message(string? input, string expectedMessage)
    {
        var exception = Should.Throw<DomainException>(() => new Username(input));

        exception.Message.ShouldBe(expectedMessage);
    }
}
