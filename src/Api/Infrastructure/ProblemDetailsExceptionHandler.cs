using System.Diagnostics;
using Application.Common.Exceptions;
using Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Infrastructure;

internal sealed class ProblemDetailsExceptionHandler(ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, problemDetails) = ex switch
        {
            NotFoundException ne => (StatusCodes.Status404NotFound, new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                Title = "The specified resource was not found.",
                Detail = ne.Message
            }),
            DomainException de => (StatusCodes.Status400BadRequest, new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = "Invalid Operation.",
                Detail = de.Message
            }),
            // Every invalid field at once, in the standard "errors" dictionary
            ValidationException ve => (StatusCodes.Status400BadRequest, new ValidationProblemDetails(
                ve.Errors.ToDictionary(e => e.Key, e => e.Value))
            {
                Status = StatusCodes.Status400BadRequest,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = ve.Message
            }),
            ConflictException ce => (StatusCodes.Status409Conflict, new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                Title = "There was a conflict.",
                Detail = ce.Message
            }),
            // Thrown by minimal API binding (missing required parameter, malformed JSON body...): a client error, not a 500
            BadHttpRequestException be => (be.StatusCode, new ProblemDetails
            {
                Status = be.StatusCode,
                Title = "Invalid request.",
                Detail = be.Message
            }),
            _ => (StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                Title = "Unexpected error",
                Extensions = { ["traceId"] = Activity.Current?.TraceId.ToString() }
            })
        };

        if (status == StatusCodes.Status500InternalServerError)
            logger.LogError(ex, "Unhandled exception on {Method} {Path}", ctx.Request.Method, ctx.Request.Path);

        ctx.Response.StatusCode = status;
        // Runtime type, not ProblemDetails: otherwise the "errors" of a ValidationProblemDetails would be dropped
        await ctx.Response.WriteAsJsonAsync(problemDetails, problemDetails.GetType(), ct);
        return true;
    }
}