using Microsoft.Extensions.DependencyInjection;
using WorkTracker.WorkItems.Domain;
using WorkTracker.WorkItems.Infrastructure;

namespace WorkTracker.WorkItems;

// The module's only public entry point. It needs an IUsersApi registration (AddUsersModule provides it).
public static class WorkItemsModule
{
    public static IServiceCollection AddWorkItemsModule(this IServiceCollection services) =>
        services.AddSingleton<IWorkItemRepository, InMemoryWorkItemRepository>();
}
