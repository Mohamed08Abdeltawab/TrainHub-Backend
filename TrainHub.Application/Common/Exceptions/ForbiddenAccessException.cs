namespace TrainHub.Application.Common.Exceptions;

/// <summary>
/// Thrown when a user attempts to access a resource they do not have permission for.
/// Maps to HTTP 403 Forbidden.
/// </summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException()
        : base("You do not have permission to access this resource.")
    {
    }

    public ForbiddenAccessException(string message) : base(message)
    {
    }
}
