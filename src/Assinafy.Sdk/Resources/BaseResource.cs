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

    private protected BaseResource(HttpClient http, string? defaultAccountId = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _defaultAccountId = defaultAccountId;
    }

    private protected string AccountId(string? explicitAccountId = null)
    {
        var id = explicitAccountId ?? _defaultAccountId;
        if (string.IsNullOrWhiteSpace(id))
            throw new ValidationException(
                "Account ID is required. Provide it as a parameter or set a default in the client.");

        return id;
    }

    private protected static string RequireId(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException($"{name} is required");

        return value;
    }

    private protected Task<T> CallAsync<T>(
        string path,
        HttpMethod method,
        object? body = null,
        IDictionary<string, string>? extraHeaders = null,
        CancellationToken cancellationToken = default)
    {
        return SendEnvelopeAsync<T>(
            () => BuildRequest(path, method, body, extraHeaders),
            cancellationToken);
    }

    private protected Task<T> CallContentAsync<T>(
        string path,
        HttpMethod method,
        HttpContent content,
        IDictionary<string, string>? extraHeaders = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        return SendEnvelopeAsync<T>(
            () => BuildContentRequest(path, method, content, extraHeaders),
            cancellationToken);
    }

    private protected Task CallContentVoidAsync(
        string path,
        HttpMethod method,
        HttpContent content,
        IDictionary<string, string>? extraHeaders = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        return SendEnvelopeAsync<object>(
            () => BuildContentRequest(path, method, content, extraHeaders),
            cancellationToken);
    }

    private protected Task CallVoidAsync(
        string path,
        HttpMethod method,
        object? body = null,
        CancellationToken cancellationToken = default)
    {
        return SendEnvelopeAsync<object>(
            () => BuildRequest(path, method, body),
            cancellationToken);
    }

    private protected async Task<byte[]> CallBinaryAsync(
        string path,
        HttpMethod method,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendAsync(
            () => new HttpRequestMessage(method, NormalizePath(path)),
            cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            await ParseEnvelopeAsync<object>(response, cancellationToken).ConfigureAwait(false);

        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }

    private protected async Task<PaginatedResult<T>> CallListAsync<T>(
        string path,
        IDictionary<string, string?>? queryParams = null,
        CancellationToken cancellationToken = default)
    {
        var url = AppendQueryString(path, queryParams);

        using var response = await SendAsync(
            () => new HttpRequestMessage(HttpMethod.Get, NormalizePath(url)),
            cancellationToken).ConfigureAwait(false);

        var data = await ParseEnvelopeAsync<List<T>>(response, cancellationToken).ConfigureAwait(false);

        return new PaginatedResult<T>
        {
            Data = data ?? [],
            Meta = ParsePaginationMeta(response.Headers),
        };
    }

    private async Task<T> SendEnvelopeAsync<T>(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        using var response = await SendAsync(requestFactory, cancellationToken).ConfigureAwait(false);
        return await ParseEnvelopeAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = requestFactory();
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
        object? body,
        IDictionary<string, string>? extraHeaders = null)
    {
        var request = new HttpRequestMessage(method, NormalizePath(path));
        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        AddHeaders(request, extraHeaders);
        return request;
    }

    private static HttpRequestMessage BuildContentRequest(
        string path,
        HttpMethod method,
        HttpContent content,
        IDictionary<string, string>? extraHeaders)
    {
        var request = new HttpRequestMessage(method, NormalizePath(path)) { Content = content };
        AddHeaders(request, extraHeaders);
        return request;
    }

    private static void AddHeaders(HttpRequestMessage request, IDictionary<string, string>? extraHeaders)
    {
        if (extraHeaders is null) return;

        foreach (var (key, value) in extraHeaders)
            request.Headers.TryAddWithoutValidation(key, value);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return path;
        return Uri.TryCreate(path, UriKind.Absolute, out _) ? path : path.TrimStart('/');
    }

    private static async Task<T> ParseEnvelopeAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(json))
        {
            if (response.IsSuccessStatusCode) return default!;
            throw new ApiException((int)response.StatusCode, response.ReasonPhrase);
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("status", out var statusEl) &&
            statusEl.ValueKind == JsonValueKind.Number)
        {
            var status = statusEl.GetInt32();
            var message = ReadMessage(root);

            if (status >= 400 || !response.IsSuccessStatusCode)
                throw new ApiException(status, message);

            if (root.TryGetProperty("data", out var dataEl))
            {
                if (dataEl.ValueKind == JsonValueKind.Null) return default!;
                return dataEl.Deserialize<T>(JsonOptions)!;
            }

            return default!;
        }

        if (!response.IsSuccessStatusCode)
            throw new ApiException((int)response.StatusCode, ReadMessage(root) ?? response.ReasonPhrase);

        return root.Deserialize<T>(JsonOptions)!;
    }

    private static string? ReadMessage(JsonElement root)
    {
        if (root.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.String)
            return msgEl.GetString();

        if (root.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
            return nameEl.GetString();

        return null;
    }

    private protected static string AppendQueryString(string path, IDictionary<string, string?>? queryParams)
    {
        if (queryParams is null || queryParams.Count == 0) return path;

        var pairs = queryParams
            .Where(kvp => kvp.Value is not null)
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
