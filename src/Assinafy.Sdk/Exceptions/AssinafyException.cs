namespace Assinafy.Sdk.Exceptions;

public class AssinafyException : Exception
{
    public AssinafyException(string message) : base(message) { }

    public AssinafyException(string message, Exception inner) : base(message, inner) { }
}
