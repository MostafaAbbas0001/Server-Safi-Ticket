namespace Safi_Ticket.Common;

/// <summary>
/// An expected failure with a message that is safe to show the user. The
/// exception handler turns it into a JSON <c>{ message }</c> response with
/// <see cref="StatusCode"/>, so controllers never need try/catch blocks.
/// </summary>
public sealed class AppException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static AppException BadRequest(string message) =>
        new(StatusCodes.Status400BadRequest, message);

    public static AppException Unauthorized(string message) =>
        new(StatusCodes.Status401Unauthorized, message);

    public static AppException Forbidden(
        string message = "You don't have access to this ticket."
    ) => new(StatusCodes.Status403Forbidden, message);

    public static AppException NotFound(string message) =>
        new(StatusCodes.Status404NotFound, message);
}
