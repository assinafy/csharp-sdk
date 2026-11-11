using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class ExpandedDocumentTests
{
    private static AssinafyClient Client(FakeHttpMessageHandler handler) =>
        new(new AssinafyClientOptions { ApiKey = "example-key", AccountId = "acc" }, FakeHttpMessageHandler.CreateClient(handler));

    [Fact]
    public async Task ExpandedWorkspaceReadsAndSignedRenders_SendTheirQueries()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/documents/doc?expand=", """
            {"status":200,"data":{"id":"doc","envelope_id":"env","envelope":{"id":"env","name":"Agreement"}}}
            """);
        handler.AddRawResponse(HttpMethod.Get, "/documents/search", "{\"status\":200,\"data\":[]}");
        handler.AddRawResponse(HttpMethod.Get, "/thumbnail", "image");
        handler.AddRawResponse(HttpMethod.Get, "/pages/page/download", "image");
        using var client = Client(handler);
        var document = await client.Documents.GetExpandedAsync("doc");
        document.EnvelopeId.Should().Be("env");
        document.Envelope!.Name.Should().Be("Agreement");
        await client.Documents.SearchWithOptionsAsync(new Dictionary<string, string?> { ["envelope_id"] = "env/1", ["search"] = "Agreement" });
        await client.Documents.ThumbnailWithOptionsAsync("doc", signed: true);
        await client.Documents.DownloadPageWithOptionsAsync("doc", "page", signed: true);

        handler.Requests.Select(r => r.RequestUri!.PathAndQuery).Should().Equal(
            "/v1/documents/doc?expand=envelope_id%2Cenvelope",
            "/v1/accounts/acc/documents/search?envelope_id=env%2F1&search=Agreement",
            "/v1/documents/doc/thumbnail?artifact=certificated",
            "/v1/documents/doc/pages/page/download?artifact=certificated");
        handler.Requests.Should().OnlyContain(r => r.Method == HttpMethod.Get && r.Headers.Contains("X-Api-Key"));
    }

    [Fact]
    public async Task SignerDocumentReads_NeverSendWorkspaceCredentialsAndEscapeAccessCodes()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/documents/doc?", """
            {"status":200,"data":{"id":"doc","name":"Agreement","status":"certificated",
             "current_signer":{"id":"signer","verification_method":"Email","notification_methods":["Email"],"completed":true},
             "is_signer_access_closed":true,"certified_copy":{"available":true,"notification_method":"Email","resends_left":null}}}
            """);
        handler.AddRawResponse(HttpMethod.Get, "/download/original", "pdf");
        handler.AddRawResponse(HttpMethod.Get, "/thumbnail", "image");
        handler.AddRawResponse(HttpMethod.Get, "/pages/page/download", "image");
        using var client = Client(handler);
        const string code = "secret&/code";
        var document = await client.Documents.GetAsSignerAsync("doc", code);
        document.CurrentSigner!.Completed.Should().BeTrue();
        document.IsSignerAccessClosed.Should().BeTrue();
        document.CertifiedCopy!.Available.Should().BeTrue();
        document.CertifiedCopy.ResendsLeft.Should().BeNull();
        await client.Documents.DownloadAsSignerAsync("doc", code);
        await client.Documents.ThumbnailAsSignerAsync("doc", code);
        await client.Documents.DownloadPageAsSignerAsync("doc", "page", code);

        handler.Requests.Select(r => r.RequestUri!.AbsolutePath).Should().Equal(
            "/v1/documents/doc", "/v1/documents/doc/download/original",
            "/v1/documents/doc/thumbnail", "/v1/documents/doc/pages/page/download");
        foreach (var request in handler.Requests)
        {
            request.Method.Should().Be(HttpMethod.Get);
            request.RequestUri!.Query.Should().Be("?signer-access-code=secret%26%2Fcode");
            request.Headers.Contains("X-Api-Key").Should().BeFalse();
            request.Headers.Authorization.Should().BeNull();
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task SignerDocumentReads_RejectMissingCode(string? code)
    {
        var handler = new FakeHttpMessageHandler();
        using var client = Client(handler);
        await Assert.ThrowsAsync<ValidationException>(() => client.Documents.GetAsSignerAsync("doc", code!));
        await Assert.ThrowsAsync<ValidationException>(() => client.Documents.DownloadAsSignerAsync("doc", code!));
        await Assert.ThrowsAsync<ValidationException>(() => client.Documents.ThumbnailAsSignerAsync("doc", code!));
        await Assert.ThrowsAsync<ValidationException>(() => client.Documents.DownloadPageAsSignerAsync("doc", "page", code!));
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Verification_PreservesSeparateSigningCertificationAndReadTimes()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/verify", """
            {"status":200,"data":{"hash":"example","is_valid":true,
             "completed_at":"2026-11-11T12:00:00Z","certificated_at":"2026-11-11T12:01:00Z","verified_at":"2026-11-11T13:00:00Z"}}
            """);
        using var client = Client(handler);
        var result = await client.Documents.VerifyAsync("example");
        result.CompletedAt.Should().Be("2026-11-11T12:00:00Z");
        result.CertificatedAt.Should().Be("2026-11-11T12:01:00Z");
        result.VerifiedAt.Should().Be("2026-11-11T13:00:00Z");
    }
}
