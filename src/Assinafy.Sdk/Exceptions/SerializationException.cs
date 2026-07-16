namespace Assinafy.Sdk.Exceptions;

/// <summary>
/// Thrown when a successful HTTP response carries a body that cannot be parsed as the
/// expected JSON envelope (for example a non-JSON body returned by an intermediary proxy
/// or CDN, or a payload whose shape does not match the deserialization target). Unsuccessful
/// responses surface as <see cref="ApiException"/> instead. Derives from
/// <see cref="AssinafyException"/> so a single <c>catch (AssinafyException)</c> covers every
/// SDK-originated failure.
/// </summary>
public sealed class SerializationException : AssinafyException
{
    /// <summary>Creates a new <see cref="SerializationException"/> with the given message and inner exception.</summary>
    public SerializationException(string message, Exception inner) : base(message, inner) { }
}
