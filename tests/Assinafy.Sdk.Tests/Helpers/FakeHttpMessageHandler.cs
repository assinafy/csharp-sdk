using System.Net;
using System.Text;
using System.Text.Json;

namespace Assinafy.Sdk.Tests.Helpers;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly List<(
        Func<HttpRequestMessage, bool> Matcher,
        Func<HttpRequestMessage, HttpResponseMessage> Responder)> _stubs = new();

    public List<HttpRequestMessage> Requests { get; } = new();
    public List<string> RequestBodies { get; } = new();

    public void AddJsonResponse(
        HttpMethod method,
        string urlContains,
        object responseBody,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        IDictionary<string, string>? headers = null)
    {
        _stubs.Add((
            req => req.Method == method &&
                   (req.RequestUri?.PathAndQuery.Contains(urlContains, StringComparison.Ordinal) ?? false),
            _ =>
            {
                var json = JsonSerializer.Serialize(responseBody);
                var response = new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json"),
                };
                if (headers != null)
                    foreach (var (key, value) in headers)
                        response.Headers.TryAddWithoutValidation(key, value);
                return response;
            }));
    }

    public void AddRawResponse(
        HttpMethod method,
        string urlContains,
        string body,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _stubs.Add((
            req => req.Method == method &&
                   (req.RequestUri?.PathAndQuery.Contains(urlContains, StringComparison.Ordinal) ?? false),
            _ => new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            }));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (request.Content != null)
        {
            var body = await request.Content.ReadAsStringAsync(cancellationToken);
            RequestBodies.Add(body);
        }
        else
        {
            RequestBodies.Add(string.Empty);
        }

        foreach (var (matcher, responder) in Enumerable.Reverse(_stubs))
        {
            if (matcher(request))
                return responder(request);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { status = 404, message = "Not found", data = (object?)null }),
                Encoding.UTF8,
                "application/json"),
        };
    }

    public static HttpClient CreateClient(FakeHttpMessageHandler handler, string baseUrl = "http://test.api/v1")
    {
        return new HttpClient(handler) { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
    }

    public static object ApiOk(object data) => new { status = 200, message = "", data };
}
