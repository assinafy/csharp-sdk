namespace Assinafy.Sdk.Exceptions;

/// <summary>
/// Thrown when an outgoing request body cannot be serialized or a successful HTTP response
/// cannot be parsed as the expected JSON envelope (for example a non-JSON body returned by an
/// intermediary proxy or CDN, or a payload whose shape does not match the deserialization target).
/// Unsuccessful responses surface as <see cref="ApiException"/> instead. Derives from
/// <see cref="AssinafyException"/> so callers can handle it alongside the other SDK-specific
/// exception types.
/// </summary>
public sealed class SerializationException : AssinafyException
{
    /// <summary>Creates a new <see cref="SerializationException"/> with the given message.</summary>
    /// <param name="message">Message describing the serialization failure.</param>
    public SerializationException(string message) : base(message) { }

    /// <summary>Creates a new <see cref="SerializationException"/> with the given message and inner exception.</summary>
    /// <param name="message">Message describing the serialization failure.</param>
    /// <param name="inner">Underlying serialization exception.</param>
    public SerializationException(string message, Exception inner) : base(message, inner) { }
}
