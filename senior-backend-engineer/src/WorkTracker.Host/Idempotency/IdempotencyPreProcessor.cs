using System.Security.Cryptography;
using System.Text.Json;
using FastEndpoints;

namespace WorkTracker.Host.Idempotency;

// Before a POST handler runs: require a key, then reserve it, replay the stored response,
// or reject the request. Sending a response here means the handler does not run.
internal sealed class IdempotencyPreProcessor(IdempotencyStore store) : IGlobalPreProcessor
{
    public const string KeyHeader = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotent-Replayed";

    public async Task PreProcessAsync(IPreProcessorContext ctx, CancellationToken ct)
    {
        var http = ctx.HttpContext;
        var key = http.Request.Headers[KeyHeader].ToString();

        if (string.IsNullOrWhiteSpace(key))
        {
            await SendProblemAsync(http, StatusCodes.Status400BadRequest, "Idempotency-Key header is required.");
            return;
        }

        var reservation = store.Reserve(key, Fingerprint(ctx));
        switch (reservation.Status)
        {
            case ReservationStatus.Reserved:
                http.ProcessorState<IdempotencyState>().ReservedKey = key;
                break;
            case ReservationStatus.Completed:
                await ReplayAsync(http, reservation.Response!, ct);
                break;
            case ReservationStatus.InProgress:
                await SendProblemAsync(http, StatusCodes.Status409Conflict,
                    "A request with this Idempotency-Key is already being processed.");
                break;
            case ReservationStatus.KeyReusedForDifferentRequest:
                await SendProblemAsync(http, StatusCodes.Status422UnprocessableEntity,
                    "Idempotency-Key was already used with a different request.");
                break;
        }
    }

    // Same route + same request body = same request.
    private static string Fingerprint(IPreProcessorContext ctx)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(ctx.Request, ctx.Request?.GetType() ?? typeof(object));
        return $"{ctx.HttpContext.Request.Path}:{Convert.ToHexString(SHA256.HashData(body))}";
    }

    private static async Task ReplayAsync(HttpContext http, StoredResponse stored, CancellationToken ct)
    {
        http.MarkResponseStart();
        http.Response.StatusCode = stored.StatusCode;
        http.Response.ContentType = "application/json; charset=utf-8";
        http.Response.Headers[ReplayedHeader] = "true";
        if (stored.Location is not null)
            http.Response.Headers.Location = stored.Location;
        await http.Response.WriteAsync(stored.Body, ct);
    }

    private static async Task SendProblemAsync(HttpContext http, int status, string detail)
    {
        http.MarkResponseStart();
        await TypedResults.Problem(detail: detail, statusCode: status).ExecuteAsync(http);
    }
}
