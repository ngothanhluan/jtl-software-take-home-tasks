namespace WorkTracker.Users.Domain;

internal sealed class User
{
    private User(Guid id, Username username)
    {
        Id = id;
        Username = username;
    }

    public Guid Id { get; }
    public Username Username { get; }

    public static User Create(Username username) => new(Guid.NewGuid(), username);
}
