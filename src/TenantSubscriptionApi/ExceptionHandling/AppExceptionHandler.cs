using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using TenantSubscriptionApi.Domain.Exceptions;

namespace TenantSubscriptionApi.ExceptionHandling;

// Maps our typed domain exceptions to the right HTTP status — pulled forward from
// M5 because M2's onboarding failures (duplicate org name, unknown plan) need a
// real 400 instead of an opaque 500 to verify cleanly over HTTP.
public class AppExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed"),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ForbiddenException => (StatusCodes.Status403Forbidden, "Forbidden"),
            LimitExceededException => (StatusCodes.Status409Conflict, "Limit exceeded"),
            _ => (0, (string?)null)
        };

        if (title is null)
            return false; // not one of ours — let the default problem-details handler produce a 500

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception.Message
        }, cancellationToken);

        return true;
    }
}
