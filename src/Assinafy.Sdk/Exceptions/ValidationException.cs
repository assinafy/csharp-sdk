namespace Assinafy.Sdk.Exceptions;

public class ValidationException : AssinafyException
{
    public IReadOnlyDictionary<string, object?>? Details { get; }

    public ValidationException(string message, IReadOnlyDictionary<string, object?>? details = null)
        : base(message)
    {
        Details = details;
    }
}
