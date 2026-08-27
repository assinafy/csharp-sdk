using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Support;

namespace Assinafy.Sdk.Resources;

/// <summary>
/// Shared HTTP, serialization, error, and pagination handling for Assinafy API resources.
/// </summary>
public abstract class BaseResource
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        Converters = { new FlexibleStringJsonConverter() },
    };

    private readonly HttpClient _http;
    private readonly string? _defaultAccountId;
    private readonly Action<HttpRequestMessage>? _authenticate;

    private protected BaseResource(
        HttpClient http,
        string? defaultAccountId = null,
        Action<HttpRequestMessage>? authenticate = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _defaultAccountId = defaultAccountId;
        _authenticate = authenticate;
    }

    private protected string AccountId(string? explicitAccountId = null)
    {
        var id = explicitAccountId ?? _defaultAccountId;
        return PathSegment(
            id,
            "Account ID",
            "Account ID is required. Provide it as a parameter or set a default in the client.");
    }

    private protected static string RequireId(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException($"{name} is required");

        return value;
    }

    private protected static string PathSegment(
        string? value,
        string name,
        string? requiredMessage = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException(requiredMessage ?? $"{name} is required");
        if (value is "." or "..")
            throw new ValidationException($"{name} is invalid");

        return Uri.EscapeDataString(value);
    }

    /// <summary>Query-parameter name carrying a signer's access code on signer-facing endpoints.</summary>
    private protected const string SignerAccessCodeParam = "signer-access-code";

    /// <summary>Builds a single-entry query dictionary carrying the signer access code.</summary>
    private protected static Dictionary<string, string?> AccessCodeQuery(string code) =>
        new() { [SignerAccessCodeParam] = code };

    /// <summary>
    /// Send a request and deserialize the envelope's required <c>data</c> into
    /// <typeparamref name="T"/>. List callers should prefer <see cref="CallListBodyAsync{T}"/>,
    /// which requires the payload to be a JSON array.
    /// </summary>
    private protected async Task<T> CallAsync<T>(
        string path,
        HttpMethod method,
        object? body = null,
        CancellationToken cancellationToken = default,
        bool authenticate = true)
    {
        var result = await SendEnvelopeAsync<T>(
            () => BuildRequest(path, method, body),
            cancellationToken,
            authenticate,
            requireData: true).ConfigureAwait(false);

        return result is null
            ? throw new SerializationException("The API response did not contain the expected data payload.")
            : result;
    }

    /// <summary>
    /// Send a request whose envelope <c>data</c> is a JSON array, returning it as a non-null
    /// read-only list. An absent or <c>null</c> payload is a response-contract error.
    /// </summary>
    private protected async Task<IReadOnlyList<T>> CallListBodyAsync<T>(
        string path,
        HttpMethod method,
        object? body = null,
        CancellationToken cancellationToken = default,
        bool authenticate = true)
    {
        var result = await SendEnvelopeAsync<List<T>>(
            () => BuildRequest(path, method, body),
            cancellationToken,
            authenticate,
            requireData: true).ConfigureAwait(false);

        return result;
    }

    private protected async Task<T> CallContentAsync<T>(
        string path,
        HttpMethod method,
        HttpContent content,
        CancellationToken cancellationToken = default,
        bool authenticate = true)
    {
        ArgumentNullException.ThrowIfNull(content);
        var result = await SendEnvelopeAsync<T>(
            () => BuildContentRequest(path, method, content),
            cancellationToken,
            authenticate,
            requireData: true).ConfigureAwait(false);

        return result is null
            ? throw new SerializationException("The API response did not contain the expected data payload.")
            : result;
    }

    private protected Task CallContentVoidAsync(
        string path,
        HttpMethod method,
        HttpContent content,
        CancellationToken cancellationToken = default,
        bool authenticate = true)
    {
        ArgumentNullException.ThrowIfNull(content);
        return SendEnvelopeAsync<object>(
            () => BuildContentRequest(path, method, content),
            cancellationToken,
            authenticate,
            requireData: false);
    }

    private protected Task CallVoidAsync(
        string path,
        HttpMethod method,
        object? body = null,
        CancellationToken cancellationToken = default,
        bool authenticate = true)
    {
        return SendEnvelopeAsync<object>(
            () => BuildRequest(path, method, body),
            cancellationToken,
            authenticate,
            requireData: false);
    }

    private protected async Task<byte[]> CallBinaryAsync(
        string path,
        HttpMethod method,
        CancellationToken cancellationToken = default,
        bool authenticate = true)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(method, NormalizePath(path)),
            cancellationToken,
            authenticate).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            await ParseEnvelopeAsync<object>(response, cancellationToken).ConfigureAwait(false);

        var mediaType = response.Content.Headers.ContentType?.MediaType;
        if (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase) ||
            mediaType?.EndsWith("+json", StringComparison.OrdinalIgnoreCase) == true)
        {
            await ParseEnvelopeAsync<object>(response, cancellationToken).ConfigureAwait(false);
            throw new SerializationException("The API returned JSON where a binary response was expected.");
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private protected async Task<PaginatedResult<T>> CallListAsync<T>(
        string path,
        IDictionary<string, string?>? queryParams = null,
        CancellationToken cancellationToken = default,
        bool authenticate = true)
    {
        var url = AppendQueryString(path, queryParams);

        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, NormalizePath(url)),
            cancellationToken,
            authenticate).ConfigureAwait(false);

        var data = await ParseEnvelopeAsync<List<T>>(
            response,
            cancellationToken,
            requireData: true).ConfigureAwait(false);

        return new PaginatedResult<T>
        {
            Data = data,
            Meta = ParsePaginationMeta(response.Headers),
        };
    }

    private async Task<T> SendEnvelopeAsync<T>(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken,
        bool authenticate,
        bool requireData)
    {
        using var response = await SendAsync(requestFactory, cancellationToken, authenticate).ConfigureAwait(false);
        return await ParseEnvelopeAsync<T>(response, cancellationToken, requireData).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken,
        bool authenticate)
    {
        try
        {
            using var request = requestFactory();
            if (authenticate)
                _authenticate?.Invoke(request);
            return await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (AssinafyException) { throw; }
        catch (HttpRequestException ex)
        {
            throw new NetworkException($"Network error: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new NetworkException("Request timed out", ex);
        }
    }

    private static HttpRequestMessage BuildRequest(
        string path,
        HttpMethod method,
        object? body)
    {
        if (body is null)
            return new HttpRequestMessage(method, NormalizePath(path));

        try
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            return new HttpRequestMessage(method, NormalizePath(path))
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new SerializationException("Failed to serialize the API request body as JSON.", ex);
        }
    }

    private static HttpRequestMessage BuildContentRequest(
        string path,
        HttpMethod method,
        HttpContent content)
    {
        return new HttpRequestMessage(method, NormalizePath(path)) { Content = content };
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ValidationException("Request path is required.");
        if (Uri.TryCreate(path, UriKind.Absolute, out _))
            throw new ValidationException("Request path must be relative to the configured API base URL.");

        return path.TrimStart('/');
    }

    private static async Task<T> ParseEnvelopeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken,
        bool requireData = false)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json))
        {
            if (response.IsSuccessStatusCode)
                throw new SerializationException("The API returned an empty success response instead of a JSON envelope.");
            throw new ApiException((int)response.StatusCode, response.ReasonPhrase);
        }

        try
        {
            return ParseEnvelope<T>(json, response, requireData);
        }
        catch (JsonException ex)
        {
            // A non-JSON body (e.g. an HTML 5xx from an intermediary proxy/CDN) or a payload whose
            // shape does not match T. Keep every failure inside the AssinafyException hierarchy rather
            // than leaking a raw System.Text.Json.JsonException.
            if (!response.IsSuccessStatusCode)
                throw new ApiException((int)response.StatusCode, response.ReasonPhrase);

            throw new SerializationException("Failed to parse the API response body as JSON.", ex);
        }
    }

    private static T ParseEnvelope<T>(string json, HttpResponseMessage response, bool requireData)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Object)
        {
            if (!response.IsSuccessStatusCode)
                throw new ApiException((int)response.StatusCode, response.ReasonPhrase);

            throw new JsonException("Expected a JSON response object.");
        }

        if (!root.TryGetProperty("status", out var statusEl) ||
            statusEl.ValueKind != JsonValueKind.Number ||
            !statusEl.TryGetInt32(out var status))
        {
            if (!response.IsSuccessStatusCode)
                throw new ApiException(
                    (int)response.StatusCode,
                    ReadMessage(root) ?? response.ReasonPhrase,
                    ReadErrorDetails(root));

            throw new JsonException("Expected the API success envelope to contain an integer status.");
        }

        var message = ReadMessage(root);
        if (status >= 400 || !response.IsSuccessStatusCode)
        {
            var errorStatus = status >= 400 ? status : (int)response.StatusCode;
            throw new ApiException(errorStatus, message, ReadErrorDetails(root));
        }

        if (!root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind == JsonValueKind.Null)
        {
            if (requireData)
                throw new JsonException("Expected the API success envelope to contain non-null data.");

            return default!;
        }

        return dataEl.Deserialize<T>(JsonOptions)!;
    }

    private static string? ReadMessage(JsonElement root)
    {
        if (root.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.String)
            return msgEl.GetString();

        if (root.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
            return nameEl.GetString();

        return null;
    }

    private static JsonElement? ReadErrorDetails(JsonElement root)
    {
        return root.TryGetProperty("data", out var data) && data.ValueKind != JsonValueKind.Null
            ? data.Clone()
            : null;
    }

    private protected static string AppendQueryString(string path, IDictionary<string, string?>? queryParams)
    {
        if (queryParams is null || queryParams.Count == 0) return path;

        var pairs = queryParams
            .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
            .Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value!)}")
            .ToArray();

        if (pairs.Length == 0) return path;

        var separator = path.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{path}{separator}{string.Join("&", pairs)}";
    }

    private static PaginationMeta? ParsePaginationMeta(HttpResponseHeaders headers)
    {
        static int? TryRead(HttpResponseHeaders h, string key) =>
            h.TryGetValues(key, out var values) &&
            int.TryParse(values.FirstOrDefault(), out var n)
                ? n
                : null;

        var currentPage = TryRead(headers, "x-pagination-current-page");
        var perPage = TryRead(headers, "x-pagination-per-page");
        var total = TryRead(headers, "x-pagination-total-count");
        var lastPage = TryRead(headers, "x-pagination-page-count");

        if (currentPage is null && perPage is null && total is null && lastPage is null)
            return null;

        return new PaginationMeta
        {
            CurrentPage = currentPage,
            PerPage = perPage,
            Total = total,
            LastPage = lastPage,
        };
    }
}
