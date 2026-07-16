namespace Assinafy.Sdk.Exceptions;

/// <summary>
/// Thrown when a request fails at the transport level before a usable response is received —
/// a connection, DNS, or TLS failure, or a request that times out. Error responses that do
/// arrive surface as <see cref="ApiException"/> instead. Derives from <see cref="AssinafyException"/>.
/// </summary>
public sealed class NetworkException : AssinafyException
{
    /// <summary>Creates a new <see cref="NetworkException"/> with the given message.</summary>
    public NetworkException(string message) : base(message) { }

    /// <summary>Creates a new <see cref="NetworkException"/> with the given message and the underlying transport exception that caused it.</summary>
    public NetworkException(string message, Exception inner) : base(message, inner) { }
}
