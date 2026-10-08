using FastEndpoints;

namespace WorkTracker.Host.Idempotency;

internal static class IdempotencyRegistration
{
    public static IServiceCollection AddIdempotency(this IServiceCollection services, IConfiguration configuration)
    {
        var window = TimeSpan.FromSeconds(configuration.GetValue("Idempotency:WindowSeconds", 10));
        return services
            .AddSingleton(TimeProvider.System)
            .AddSingleton(sp => new IdempotencyStore(sp.GetRequiredService<TimeProvider>(), window));
    }

    // Only POSTs create things; GETs are safe to repeat.
    public static void UseIdempotencyOnPosts(this EndpointDefinition endpoint)
    {
        if (!endpoint.Verbs.Contains("POST"))
            return;

        endpoint.PreProcessor<IdempotencyPreProcessor>(Order.Before);
        endpoint.PostProcessor<IdempotencyPostProcessor>(Order.After);
    }
}
