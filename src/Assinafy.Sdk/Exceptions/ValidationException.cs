namespace Assinafy.Sdk.Exceptions;

/// <summary>
/// Thrown when the SDK rejects a request client-side, before or independently of any HTTP call —
/// a missing or invalid argument, a malformed request, or a polled operation that ends in a
/// failure state. Errors reported by the API itself surface as <see cref="ApiException"/> instead.
/// Derives from <see cref="AssinafyException"/>.
/// </summary>
public sealed class ValidationException : AssinafyException
{
    /// <summary>Optional field-level validation details keyed by field name, or <see langword="null"/> when none were supplied.</summary>
    public IReadOnlyDictionary<string, object?>? Details { get; }

    /// <summary>Creates a new <see cref="ValidationException"/> with the given message and optional field-level details.</summary>
    /// <param name="message">Message describing the validation failure.</param>
    /// <param name="details">Optional field-level validation details.</param>
    public ValidationException(string message, IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        Details = details;
    }

    /// <summary>Creates a new <see cref="ValidationException"/> with details and the underlying exception that caused it.</summary>
    /// <param name="message">Message describing the validation failure.</param>
    /// <param name="details">Optional field-level validation details.</param>
    /// <param name="inner">Underlying exception that caused the validation failure.</param>
    public ValidationException(
        string message,
        IReadOnlyDictionary<string, object?>? details,
        Exception inner)
        : base(message, inner)
    {
        Details = details;
    }
}
