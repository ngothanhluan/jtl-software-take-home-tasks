using WorkTracker.Shared;
using WorkTracker.Users.Features.CreateUser;
using WorkTracker.Users.Infrastructure;

namespace WorkTracker.Users.Tests.Features;

public class CreateUserHandlerTests
{
    private readonly InMemoryUserRepository _repository = new();
    private readonly CreateUserHandler _handler;

    public CreateUserHandlerTests() => _handler = new CreateUserHandler(_repository);

    [Fact]
    public async Task TC_U03_Create_user_returns_response_and_saves_the_user()
    {
        var response = await _handler.ExecuteAsync(new CreateUserCommand("alice"), CancellationToken.None);

        response.Id.ShouldNotBe(Guid.Empty);
        response.Username.ShouldBe("alice");
        var saved = await _repository.GetByIdAsync(response.Id, CancellationToken.None);
        saved.ShouldNotBeNull().Username.Value.ShouldBe("alice");
    }

    [Fact]
    public async Task TC_U04_Duplicate_username_ignoring_case_throws_ConflictException()
    {
        await _handler.ExecuteAsync(new CreateUserCommand("alice"), CancellationToken.None);

        var exception = await Should.ThrowAsync<ConflictException>(
            () => _handler.ExecuteAsync(new CreateUserCommand("ALICE"), CancellationToken.None));

        exception.Message.ShouldBe("Username 'ALICE' already exists.");
    }
}
