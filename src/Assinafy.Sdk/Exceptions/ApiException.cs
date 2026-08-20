using System.Text.Json;

namespace Assinafy.Sdk.Exceptions;

/// <summary>
/// Thrown when the API returns an error response — an HTTP status of 400 or greater, or an
/// envelope whose <c>status</c> field is 400 or greater. Client-side, transport, and parsing
/// failures surface as <see cref="ValidationException"/>, <see cref="NetworkException"/>, and
/// <see cref="SerializationException"/> respectively. Derives from <see cref="AssinafyException"/>.
/// </summary>
public sealed class ApiException : AssinafyException
{
    /// <summary>The HTTP status code (or envelope <c>status</c> value) returned by the API; always 400 or greater.</summary>
    public int StatusCode { get; }

    /// <summary>Human-readable error message reported by the API, or <see langword="null"/> when the response carried none.</summary>
    public string? ApiMessage { get; }

    /// <summary>Structured error details from the envelope's <c>data</c> field, or <see langword="null"/> when absent.</summary>
    public JsonElement? Details { get; }

    /// <summary>Creates a new <see cref="ApiException"/> for the given status code and optional API-supplied message.</summary>
    public ApiException(int statusCode, string? apiMessage = null)
        : this(statusCode, apiMessage, null) { }

    /// <summary>Creates a new <see cref="ApiException"/> for the given status code, API-supplied message, and structured details.</summary>
    public ApiException(int statusCode, string? apiMessage, JsonElement? details)
        : base($"API error {statusCode}{(apiMessage != null ? $": {apiMessage}" : string.Empty)}")
    {
        StatusCode = statusCode;
        ApiMessage = apiMessage;
        Details = details;
    }
}
