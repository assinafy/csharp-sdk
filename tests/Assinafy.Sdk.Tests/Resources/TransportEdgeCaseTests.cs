using System.Net;
using System.Text;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class TransportEdgeCaseTests
{
    [Theory]
    [InlineData("Basic realm=\"workspace\", error=\"other\", Bearer error=\"insufficient_scope\", scope=\"documents:read\", Basic realm=\"proxy\", scope=\"ignored\", error=\"wrong\"")]
    [InlineData("Bearer error=insufficient_scope, scope=\"documents:read\"")]
    public async Task BearerChallenge_ReadsOnlyItsOwnParameters(string challenge)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/document", new { status = 403 },
            HttpStatusCode.Forbidden, new Dictionary<string, string> { ["WWW-Authenticate"] = challenge });
        using var http = FakeHttpMessageHandler.CreateClient(handler);

        var act = () => new DocumentResource(http).GetAsync("document");
        var error = (await act.Should().ThrowAsync<OAuthException>()).Which;

        error.Error.Should().Be("insufficient_scope");
        error.Scope.Should().Be("documents:read");
    }

    [Theory]
    [InlineData("Basic realm=\"Bearer\", error=\"invalid_token\"")]
    [InlineData("NotBearer error=\"invalid_token\"")]
    [InlineData("Basic realm=\"proxy, Bearer error=invalid_token\"")]
    public async Task OtherChallenges_DoNotBecomeOAuthErrors(string challenge)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/document", new { status = 403 },
            HttpStatusCode.Forbidden, new Dictionary<string, string> { ["WWW-Authenticate"] = challenge });
        using var http = FakeHttpMessageHandler.CreateClient(handler);

        var act = () => new DocumentResource(http).GetAsync("document");

        (await act.Should().ThrowAsync<ApiException>()).Which.Should().NotBeOfType<OAuthException>();
    }

    [Fact]
    public async Task BearerChallenge_UnescapesQuotedValuesWithoutSplittingQuotedCommas()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/document", new { status = 401 },
            HttpStatusCode.Unauthorized, new Dictionary<string, string>
            {
                ["WWW-Authenticate"] = "Bearer error=\"invalid_token\", error_description=\"A \\\"quoted\\\", \\\\ path\", scope=\"documents:read\"",
            });
        using var http = FakeHttpMessageHandler.CreateClient(handler);

        var act = () => new DocumentResource(http).GetAsync("document");
        var error = (await act.Should().ThrowAsync<OAuthException>()).Which;

        error.ErrorDescription.Should().Be("A \"quoted\", \\ path");
        error.Scope.Should().Be("documents:read");
    }

    [Theory]
    [InlineData(false, HttpStatusCode.OK)]
    [InlineData(true, HttpStatusCode.OK)]
    [InlineData(false, HttpStatusCode.TooManyRequests)]
    [InlineData(true, HttpStatusCode.TooManyRequests)]
    public async Task UnsupportedResponseCharset_StaysWithinSdkExceptions(bool oauth, HttpStatusCode status)
    {
        using var http = new HttpClient(new UnsupportedCharsetHandler(status))
        {
            BaseAddress = new Uri("https://api.assinafy.com.br/v1/"),
        };
        Func<Task> act = oauth
            ? () => new OAuthResource(http).RefreshTokenAsync(new OAuthRefreshRequest
            {
                ClientId = "client",
                RefreshToken = "refresh-token",
            })
            : () => new DocumentResource(http).GetAsync("document");

        if (status == HttpStatusCode.OK)
        {
            (await act.Should().ThrowAsync<SerializationException>()).Which.InnerException
                .Should().BeOfType<InvalidOperationException>();
        }
        else
        {
            var error = (await act.Should().ThrowAsync<ApiException>()).Which;
            error.StatusCode.Should().Be(429);
            error.RetryAfter.Should().Be(TimeSpan.FromSeconds(30));
        }
    }

    [Theory]
    [InlineData("""{"status":400,"message":"Invalid contract","errors":[{"field":"end_date","code":"InvalidDate"}]}""", "errors")]
    [InlineData("""{"status":409,"message":"Contract changed","code":"ContractChanged"}""", "code")]
    [InlineData("""{"status":400,"message":"Invalid contract","data":{"document_id":"document"},"errors":[{"field":"end_date","code":"InvalidDate"}]}""", "errors")]
    public async Task StructuredTopLevelErrors_RetainTheCompletePayload(string body, string detailProperty)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/documents/document", body, HttpStatusCode.BadRequest);
        using var http = FakeHttpMessageHandler.CreateClient(handler);

        var act = () => new DocumentResource(http).GetAsync("document");
        var error = (await act.Should().ThrowAsync<ApiException>()).Which;

        error.Details.Should().NotBeNull();
        error.Details!.Value.GetRawText().Should().Be(body);
        error.Details.Value.TryGetProperty(detailProperty, out _).Should().BeTrue();
    }

    private sealed class UnsupportedCharsetHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status)
            {
                Content = new StringContent("{}", Encoding.UTF8, "application/json"),
            };
            response.Content.Headers.ContentType!.CharSet = "unsupported-charset";
            response.Headers.TryAddWithoutValidation("Retry-After", "30");
            return Task.FromResult(response);
        }
    }
}
