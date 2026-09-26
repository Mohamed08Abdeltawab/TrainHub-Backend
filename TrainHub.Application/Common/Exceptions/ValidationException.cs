namespace TrainHub.Application.Common.Exceptions;

/// <summary>
/// Thrown when one or more validation errors occur on an incoming request.
/// Maps to HTTP 422 Unprocessable Entity (or 400 Bad Request).
/// Carries per-field error details suitable for front-end form binding.
/// </summary>
public class ValidationException : Exception
{
    /// <summary>
    /// A dictionary mapping field names to their validation error messages.
    /// </summary>
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException()
        : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string message, IDictionary<string, string[]> errors)
        : base(message)
    {
        Errors = errors;
    }
}
