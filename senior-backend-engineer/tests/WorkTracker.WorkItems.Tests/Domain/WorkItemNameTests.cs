using WorkTracker.Shared;
using WorkTracker.WorkItems.Domain;

namespace WorkTracker.WorkItems.Tests.Domain;

public class WorkItemNameTests
{
    [Fact]
    public void TC_W01_Valid_name_is_accepted_and_trimmed()
    {
        new WorkItemName("  Write README  ").Value.ShouldBe("Write README");
        new WorkItemName(new string('x', 200)).Value.Length.ShouldBe(200);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TC_W02_Missing_name_throws_DomainException(string? input)
    {
        var exception = Should.Throw<DomainException>(() => new WorkItemName(input));

        exception.Message.ShouldBe("Work item name is required.");
    }

    [Fact]
    public void TC_W02_Name_over_200_characters_throws_DomainException()
    {
        var exception = Should.Throw<DomainException>(() => new WorkItemName(new string('x', 201)));

        exception.Message.ShouldBe("Work item name must be at most 200 characters.");
    }
}
