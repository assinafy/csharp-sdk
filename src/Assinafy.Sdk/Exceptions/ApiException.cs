namespace Assinafy.Sdk.Exceptions;

public class ApiException : AssinafyException
{
    public int StatusCode { get; }
    public string? ApiMessage { get; }

    public ApiException(int statusCode, string? apiMessage = null)
        : base($"API error {statusCode}{(apiMessage != null ? $": {apiMessage}" : string.Empty)}")
    {
        StatusCode = statusCode;
        ApiMessage = apiMessage;
    }
}
