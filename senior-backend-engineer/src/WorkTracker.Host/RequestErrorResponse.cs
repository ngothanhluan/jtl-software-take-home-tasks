using FluentValidation.Results;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace WorkTracker.Host;

// Builds the response when FastEndpoints cannot bind a request (malformed JSON, a value of the wrong type).
// Same problem details shape as ProblemDetailsExceptionHandler, with one plain message
// instead of the JSON parser's internal text.
internal static class RequestErrorResponse
{
    // FastEndpoints' default SerializerErrorsField: the JSON could not be parsed at all.
    private const string SerializerErrorsField = "SerializerErrors";

    public static ProblemDetails Build(List<ValidationFailure> failures, HttpContext _, int statusCode)
    {
        var failure = failures[0];
        var detail = failure.PropertyName == SerializerErrorsField
            ? "The request body is not valid JSON."
            : $"'{failure.PropertyName}' has an invalid value.";

        return TypedResults.Problem(detail: detail, statusCode: statusCode).ProblemDetails;
    }
}
