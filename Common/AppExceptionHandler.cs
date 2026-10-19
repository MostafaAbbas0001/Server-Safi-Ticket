using Microsoft.AspNetCore.Diagnostics;

namespace Safi_Ticket.Common;

/// <summary>Writes every error as <c>{ message }</c> JSON with a fitting status code.</summary>
public sealed class AppExceptionHandler(ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var (statusCode, message) = exception switch
        {
            AppException appException => (appException.StatusCode, appException.Message),
            _ => (StatusCodes.Status500InternalServerError, "Something went wrong on the server."),
        };

        if (statusCode >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                exception,
                "Unhandled error for {Method} {Path}.",
                httpContext.Request.Method,
                httpContext.Request.Path
            );
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(new { message }, cancellationToken);
        return true;
    }
}
