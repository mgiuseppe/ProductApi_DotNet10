using System;
using Application.Common.Exceptions;
using Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Infrastructure;

internal sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        var (status, problemDetails) = ex switch
        {
            NotFoundException ne => (StatusCodes.Status404NotFound, new ProblemDetails {
                Status = StatusCodes.Status404NotFound,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.5",
                Title = "The specified resource was not found.",
                Detail = ne.Message
            }),
            DomainException de => (StatusCodes.Status400BadRequest, new ProblemDetails {
                Status = StatusCodes.Status400BadRequest,
                Type = "ttps://tools.ietf.org/html/rfc9110#section-15.5.1",
                Title = "Invalid Operation.",
                Detail = de.Message
            }),
            ConflictException ce => (StatusCodes.Status409Conflict, new ProblemDetails {
                Status = StatusCodes.Status409Conflict,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                Title = "There was a conflict.",
                Detail = ce.Message
            }),
            _ => (StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                Title = "Unexpected error"
            })
        };

        ctx.Response.StatusCode = status;
        await ctx.Response.WriteAsJsonAsync(problemDetails, ct);
        return true;
    }
}