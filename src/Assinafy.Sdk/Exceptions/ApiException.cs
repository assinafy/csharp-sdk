using System.Text.Json;

namespace Assinafy.Sdk.Exceptions;

/// <summary>
/// Thrown when the API returns a non-success HTTP response, or an
/// envelope whose <c>status</c> field is 400 or greater. Client-side, transport, and parsing
/// failures surface as <see cref="ValidationException"/>, <see cref="NetworkException"/>, and
/// <see cref="SerializationException"/> respectively. Derives from <see cref="AssinafyException"/>.
/// </summary>
/// <remarks>
/// <see cref="OAuthException"/> derives from this type for the failures that carry a
/// machine-readable OAuth error code, so catching <see cref="ApiException"/> still covers them.
/// </remarks>
public class ApiException : AssinafyException
{
    /// <summary>The non-success HTTP status code, or an envelope <c>status</c> value of 400 or greater.</summary>
    public int StatusCode { get; }

    /// <summary>Human-readable error message reported by the API, or <see langword="null"/> when the response carried none.</summary>
    public string? ApiMessage { get; }

    /// <summary>Structured error details from <c>data</c>, or the full error object when it carries top-level deletion <c>restrictions</c>; <see langword="null"/> when absent.</summary>
    public JsonElement? Details { get; }

    /// <summary>
    /// Requests allowed in the current rate-limit window, from the <c>X-Rate-Limit-Limit</c> response
    /// header; <see langword="null"/> when the response did not carry it.
    /// </summary>
    public int? RateLimitLimit { get; internal set; }

    /// <summary>
    /// Requests left in the current rate-limit window, from the <c>X-Rate-Limit-Remaining</c> response
    /// header; <see langword="null"/> when the response did not carry it.
    /// </summary>
    public int? RateLimitRemaining { get; internal set; }

    /// <summary>
    /// Time until the rate-limit window resets, from the <c>X-Rate-Limit-Reset</c> response header
    /// (seconds); <see langword="null"/> when the response did not carry it.
    /// </summary>
    public TimeSpan? RateLimitReset { get; internal set; }

    /// <summary>
    /// How long to wait before retrying, from the <c>Retry-After</c> header the API sends with
    /// <c>429 Too Many Requests</c> (seconds, or an HTTP date converted to a delay);
    /// <see langword="null"/> when the response did not carry it. The SDK never retries on its own.
    /// </summary>
    public TimeSpan? RetryAfter { get; internal set; }

    /// <summary>Creates a new <see cref="ApiException"/> for the given status code and optional API-supplied message.</summary>
    /// <param name="statusCode">HTTP or envelope status code reported by the API.</param>
    /// <param name="apiMessage">Optional human-readable message reported by the API.</param>
    public ApiException(int statusCode, string? apiMessage = null)
        : this(statusCode, apiMessage, null) { }

    /// <summary>Creates a new <see cref="ApiException"/> for the given status code, API-supplied message, and structured details.</summary>
    /// <param name="statusCode">HTTP or envelope status code reported by the API.</param>
    /// <param name="apiMessage">Optional human-readable message reported by the API.</param>
    /// <param name="details">Optional structured data or deletion-restriction error object.</param>
    public ApiException(int statusCode, string? apiMessage, JsonElement? details)
        : base($"API error {statusCode}{(apiMessage != null ? $": {apiMessage}" : string.Empty)}")
    {
        StatusCode = statusCode;
        ApiMessage = apiMessage;
        Details = details;
    }
}
