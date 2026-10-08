using Microsoft.AspNetCore.Diagnostics;
using WorkTracker.Shared;

namespace WorkTracker.Host;

// Turns the Shared exceptions into RFC 9457 problem details with the rule's own message.
// Anything else is a generic 500 that exposes nothing internal.
// TypedResults.Problem always writes the body, whatever the client's Accept header says.
internal sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        var (status, detail) = exception switch
        {
            DomainException => (StatusCodes.Status400BadRequest, exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, exception.Message),
            BusinessRuleViolationException => (StatusCodes.Status422UnprocessableEntity, exception.Message),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        await TypedResults.Problem(detail: detail, statusCode: status).ExecuteAsync(httpContext);
        return true;
    }
}
