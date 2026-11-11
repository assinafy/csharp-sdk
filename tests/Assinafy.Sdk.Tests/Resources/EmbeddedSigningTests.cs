using System.Net;
using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class EmbeddedSigningTests
{
    [Fact]
    public async Task OriginsAndSession_UseWorkspaceCredentialsAndDocumentedPayloads()
    {
        var handler = new FakeHttpMessageHandler();
        var origin = new { id = "origin-1", origin = "https://app.example.com", created_at = "2026-11-04T12:00:00Z" };
        handler.AddJsonResponse(HttpMethod.Get, "/embed-origins", FakeHttpMessageHandler.ApiOk(new[] { origin }));
        handler.AddJsonResponse(HttpMethod.Post, "/embed-origins", FakeHttpMessageHandler.ApiOk(origin));
        handler.AddJsonResponse(HttpMethod.Put, "/embed-origins/", FakeHttpMessageHandler.ApiOk(origin));
        handler.AddJsonResponse(HttpMethod.Delete, "/embed-origins/", FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));
        handler.AddJsonResponse(HttpMethod.Post, "/embed-sessions", FakeHttpMessageHandler.ApiOk(new { url = "https://app.example.com/embed/secret", expires_at = "2026-11-04T12:05:00Z" }));
        var resource = new EmbedResource(FakeHttpMessageHandler.CreateClient(handler), "account/1",
            request => request.Headers.Add("X-Api-Key", "workspace-key"));

        (await resource.ListOriginsAsync()).Single().Origin.Should().Be(origin.origin);
        (await resource.AddOriginAsync(origin.origin)).Id.Should().Be(origin.id);
        (await resource.UpdateOriginAsync("origin/1", origin.origin)).Id.Should().Be(origin.id);
        await resource.DeleteOriginAsync("origin/1");
        (await resource.CreateSessionAsync("doc/1", "assignment/1", "signer/1")).Url.Should().Contain("/embed/secret");

        handler.Requests.Select(r => r.Method.Method).Should().Equal("GET", "POST", "PUT", "DELETE", "POST");
        handler.Requests.Select(r => r.RequestUri!.PathAndQuery).Should().Equal(
            "/v1/accounts/account%2F1/embed-origins", "/v1/accounts/account%2F1/embed-origins",
            "/v1/accounts/account%2F1/embed-origins/origin%2F1", "/v1/accounts/account%2F1/embed-origins/origin%2F1",
            "/v1/documents/doc%2F1/assignments/assignment%2F1/signers/signer%2F1/embed-sessions");
        handler.Requests.Should().OnlyContain(r => r.Headers.Contains("X-Api-Key"));
        using var body = JsonDocument.Parse(handler.RequestBodies[1]);
        body.RootElement.EnumerateObject().Should().ContainSingle();
        body.RootElement.GetProperty("origin").GetString().Should().Be(origin.origin);
        handler.RequestBodies[2].Should().Be(handler.RequestBodies[1]);
        handler.RequestBodies[4].Should().BeEmpty();
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://app.example.com/path")]
    [InlineData("https://app.example.com/path/..")]
    [InlineData("https://app.example.com?query=1")]
    [InlineData("https://app.example.com#fragment")]
    [InlineData("https://user@app.example.com")]
    [InlineData("https://*.example.com")]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("https://example.com\\")]
    public async Task InvalidOrigins_AreRejectedBeforeTransport(string origin)
    {
        var handler = new FakeHttpMessageHandler();
        var resource = new EmbedResource(FakeHttpMessageHandler.CreateClient(handler), "account");
        await ((Func<Task>)(() => resource.AddOriginAsync(origin))).Should().ThrowAsync<ValidationException>();
        await ((Func<Task>)(() => resource.UpdateOriginAsync("origin", origin))).Should().ThrowAsync<ValidationException>();
        handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("http://localhost:3000")]
    [InlineData("http://127.0.0.1:3000")]
    [InlineData("https://APP.example.com:443/")]
    public async Task SupportedOrigins_AreSentForServerNormalization(string origin)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/embed-origins", FakeHttpMessageHandler.ApiOk(new { id = "origin", origin }));
        var resource = new EmbedResource(FakeHttpMessageHandler.CreateClient(handler), "account");
        (await resource.AddOriginAsync(origin)).Origin.Should().Be(origin);
    }

    [Fact]
    public async Task SessionExchange_ConsumesOnlyTheTokenAndPreservesTheReturnedCredential()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/embed/sessions/token%2Fvalue/exchange", FakeHttpMessageHandler.ApiOk(new
        {
            signer_access_code = "secret-code",
            signer_id = "signer",
            document_id = "doc",
            verification_method = "Email",
            embed_origins = new[] { "https://app.example.com" },
        }));
        var resource = new EmbedResource(FakeHttpMessageHandler.CreateClient(handler), "account",
            request => request.Headers.Add("X-Api-Key", "must-not-leak"));
        var result = await resource.ExchangeSessionAsync("token/value", "https://app.example.com");
        result.SignerAccessCode.Should().Be("secret-code");
        result.EmbedOrigins.Should().ContainSingle().Which.Should().Be("https://app.example.com");
        result.ToString().Should().NotContain("secret-code");
        handler.Requests.Single().Headers.Contains("X-Api-Key").Should().BeFalse();
        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        body.RootElement.GetProperty("parent_origin").GetString().Should().Be("https://app.example.com");
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.Gone)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task SessionExchange_DoesNotRetryASpentToken(HttpStatusCode status)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/exchange", new { status = (int)status, message = "Unavailable", code = "embed_session_invalid" }, status);
        var resource = new EmbedResource(FakeHttpMessageHandler.CreateClient(handler));
        var exception = await ((Func<Task>)(() => resource.ExchangeSessionAsync("token"))).Should().ThrowAsync<ApiException>();
        exception.Which.StatusCode.Should().Be((int)status);
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public void EmbedSession_FormattingRedactsBearerUrl()
    {
        new EmbedSession { Url = "https://app.example.com/embed/secret" }.ToString().Should().NotContain("secret");
    }
}
