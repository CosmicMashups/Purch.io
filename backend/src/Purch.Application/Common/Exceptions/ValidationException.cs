namespace Purch.Application.Common.Exceptions;

/// <summary>The caller's input failed validation — maps to 400 Bad Request.</summary>
public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : AppException("One or more validation errors occurred.")
{
    public ValidationException(string field, string message)
        : this(new Dictionary<string, string[]> { [field] = [message] })
    {
    }

    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}
