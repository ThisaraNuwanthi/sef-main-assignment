using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace SkcaEnrol.Api.Common;

/// <summary>
/// One place that turns any exception into an RFC 7807 ProblemDetails response.
/// Known app exceptions keep their message; anything unexpected becomes a
/// generic 500 so stack traces and SQL never leak to the client.
/// </summary>
public class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception exception, CancellationToken ct)
    {
        var (status, title, detail) = exception switch
        {
            AppException app => (app.StatusCode, app.Title, app.Message),

            // Database constraints are the last line of defence; report them as client errors.
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (StatusCodes.Status409Conflict, "Conflict", "A record with the same unique value already exists."),
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.CheckViolation } pg } =>
                (StatusCodes.Status400BadRequest, "Invalid request", $"A database rule was broken: {pg.ConstraintName}."),
            DbUpdateConcurrencyException =>
                (StatusCodes.Status409Conflict, "Conflict", "The record was changed by someone else. Reload and try again."),

            _ => (StatusCodes.Status500InternalServerError, "Server error", "Something went wrong. Please try again later.")
        };

        if (status >= 500)
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", http.Request.Method, http.Request.Path);
        else
            logger.LogWarning("Request failed with {Status}: {Detail}", status, detail);

        http.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }
}
