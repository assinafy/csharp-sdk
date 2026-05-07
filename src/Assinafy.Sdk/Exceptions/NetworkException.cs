namespace Assinafy.Sdk.Exceptions;

public class NetworkException : AssinafyException
{
    public NetworkException(string message) : base(message) { }

    public NetworkException(string message, Exception inner) : base(message, inner) { }
}
