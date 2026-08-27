namespace Assinafy.Sdk.Exceptions;

/// <summary>
/// Base type for API, transport, serialization, and SDK validation failures. Standard .NET
/// argument, cancellation, and disposal exceptions retain their platform types. Catch a derived type
/// (<see cref="ApiException"/>, <see cref="NetworkException"/>, <see cref="ValidationException"/>,
/// <see cref="SerializationException"/>) to distinguish the cause.
/// </summary>
public class AssinafyException : Exception
{
    /// <summary>Creates a new <see cref="AssinafyException"/> with the given message.</summary>
    /// <param name="message">Message describing the failure.</param>
    public AssinafyException(string message) : base(message) { }

    /// <summary>Creates a new <see cref="AssinafyException"/> with the given message and the underlying exception that caused it.</summary>
    /// <param name="message">Message describing the failure.</param>
    /// <param name="inner">Underlying exception that caused the failure.</param>
    public AssinafyException(string message, Exception inner) : base(message, inner) { }
}
