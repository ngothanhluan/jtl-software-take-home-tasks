using System.Text.Json;
using FastEndpoints;

namespace WorkTracker.Host.Idempotency;

// After a POST handler: store a successful response for replay, or release the key on failure.
internal sealed class IdempotencyPostProcessor(IdempotencyStore store) : IGlobalPostProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task PostProcessAsync(IPostProcessorContext ctx, CancellationToken ct)
    {
        var key = ctx.HttpContext.ProcessorState<IdempotencyState>().ReservedKey;
        if (key is null)
            return Task.CompletedTask; // this request did not reserve a key (missing, replayed or rejected)

        var response = ctx.HttpContext.Response;
        if (ctx.HasExceptionOccurred || response.StatusCode >= StatusCodes.Status400BadRequest)
        {
            store.Release(key);
            return Task.CompletedTask;
        }

        var body = JsonSerializer.Serialize(ctx.Response, ctx.Response?.GetType() ?? typeof(object), JsonOptions);
        store.Complete(key, new StoredResponse(response.StatusCode, response.Headers.Location.FirstOrDefault(), body));
        return Task.CompletedTask;
    }
}
