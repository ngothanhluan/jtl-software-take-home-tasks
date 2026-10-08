using Microsoft.Extensions.DependencyInjection;
using WorkTracker.Users.Contracts;
using WorkTracker.Users.Domain;
using WorkTracker.Users.Infrastructure;

namespace WorkTracker.Users;

// The module's only public entry point besides WorkTracker.Users.Contracts.
public static class UsersModule
{
    public static IServiceCollection AddUsersModule(this IServiceCollection services) =>
        services
            .AddSingleton<IUserRepository, InMemoryUserRepository>()
            .AddSingleton<IUsersApi, UsersApi>();
}
