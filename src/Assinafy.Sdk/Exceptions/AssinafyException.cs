namespace Assinafy.Sdk.Exceptions;

/// <summary>
/// Base type for every exception the SDK raises. Catch <c>AssinafyException</c> to handle
/// all SDK-originated failures with a single handler; catch a derived type
/// (<see cref="ApiException"/>, <see cref="NetworkException"/>, <see cref="ValidationException"/>,
/// <see cref="SerializationException"/>) to distinguish the cause.
/// </summary>
public class AssinafyException : Exception
{
    /// <summary>Creates a new <see cref="AssinafyException"/> with the given message.</summary>
    public AssinafyException(string message) : base(message) { }

    /// <summary>Creates a new <see cref="AssinafyException"/> with the given message and the underlying exception that caused it.</summary>
    public AssinafyException(string message, Exception inner) : base(message, inner) { }
}
