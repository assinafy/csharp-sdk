using System.Text.Json;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class SigningResourceTests
{
    private static SigningResource CreateResource(FakeHttpMessageHandler handler)
        => new(FakeHttpMessageHandler.CreateClient(handler));

    [Fact]
    public async Task Get_AddsSignerAccessCodeWithoutUserCredentials()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "signer-access-code=access",
            FakeHttpMessageHandler.ApiOk(new { id = "doc-1", name = "contract.pdf", status = "pending_signature", created_at = "2026-01-01", updated_at = "2026-01-01" }));

        var resource = new SigningResource(
            FakeHttpMessageHandler.CreateClient(handler),
            request => request.Headers.Add("X-Api-Key", "must-not-leak"));
        var result = await resource.GetAsync("access");

        result.Id.Should().Be("doc-1");
        handler.Requests.Should().Contain(r => r.RequestUri!.PathAndQuery.Contains("/sign?signer-access-code=access"));
        handler.Requests.Single().Headers.Contains("X-Api-Key").Should().BeFalse();
    }

    [Fact]
    public async Task Sign_PostsCollectValues()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/documents/doc-1/assignments/asg-1",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));

        var resource = CreateResource(handler);
        await resource.SignAsync(
            "doc-1",
            "asg-1",
            "access",
            [new SignAssignmentValue { ItemId = "item", FieldId = "field", PageId = "page", Value = "value" }]);

        var body = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0));
        body.RootElement[0].GetProperty("itemId").GetString().Should().Be("item");
        body.RootElement[0].GetProperty("fieldId").GetString().Should().Be("field");
        body.RootElement[0].GetProperty("pageId").GetString().Should().Be("page");
        body.RootElement[0].GetProperty("value").GetString().Should().Be("value");
    }

    [Fact]
    public async Task CertificateSigning_UsesSignerCodeInQueryAndBodyWithoutUserCredentials()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/signers/certificate/start?signer-access-code=access",
            FakeHttpMessageHandler.ApiOk(new { token = "pki-token" }));
        handler.AddJsonResponse(HttpMethod.Post, "/signers/certificate/complete?signer-access-code=access",
            FakeHttpMessageHandler.ApiOk(new { signerName = "Certificate Signer" }));
        var resource = new SigningResource(
            FakeHttpMessageHandler.CreateClient(handler),
            request => request.Headers.Add("X-Api-Key", "must-not-leak"));

        var started = await resource.StartCertificateAsync("access");
        var completed = await resource.CompleteCertificateAsync("access", started.Token);

        started.Token.Should().Be("pki-token");
        completed.SignerName.Should().Be("Certificate Signer");
        handler.Requests.Should().OnlyContain(request => !request.Headers.Contains("X-Api-Key"));
        handler.Requests[0].RequestUri!.PathAndQuery.Should().Be(
            "/v1/signers/certificate/start?signer-access-code=access");
        handler.Requests[1].RequestUri!.PathAndQuery.Should().Be(
            "/v1/signers/certificate/complete?signer-access-code=access");

        using var startBody = JsonDocument.Parse(handler.RequestBodies[0]);
        startBody.RootElement.EnumerateObject().Should().ContainSingle();
        startBody.RootElement.GetProperty("signer-access-code").GetString().Should().Be("access");

        using var completeBody = JsonDocument.Parse(handler.RequestBodies[1]);
        completeBody.RootElement.EnumerateObject().Should().HaveCount(2);
        completeBody.RootElement.GetProperty("signer-access-code").GetString().Should().Be("access");
        completeBody.RootElement.GetProperty("token").GetString().Should().Be("pki-token");
    }

    [Fact]
    public async Task Decline_UsesRejectEndpoint()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/reject?signer-access-code=access",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));

        var resource = CreateResource(handler);
        await resource.DeclineAsync("doc-1", "asg-1", "access", "No");

        handler.Requests.Should().Contain(r =>
            r.RequestUri!.PathAndQuery.Contains("/documents/doc-1/assignments/asg-1/reject?signer-access-code=access"));
    }

    [Fact]
    public async Task ListDocuments_PreservesExplicitLegacyFilters()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/signers/signer-1/documents",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));

        var resource = CreateResource(handler);
        await resource.ListDocumentsAsync(
            "signer-1",
            "access",
            new SignerDocumentListParams
            {
                Status = "pending_signature",
                Method = "virtual",
                Search = "ignored",
                Sort = "name",
                Page = 2,
                PerPage = 50,
            });

        var query = handler.Requests.Single().RequestUri!.Query;
        query.Should().Contain("signer-access-code=access");
        query.Should().Contain("page=2");
        query.Should().Contain("per-page=50");
        query.Should().Contain("status=pending_signature");
        query.Should().Contain("method=virtual");
        query.Should().Contain("search=ignored");
        query.Should().Contain("sort=name");
    }

    [Fact]
    public async Task SearchDocuments_PreservesExplicitLegacyFiltersAndPagination()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/signers/signer-1/documents/search",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));

        await CreateResource(handler).SearchDocumentsAsync(
            "signer-1",
            "access",
            new SignerDocumentListParams
            {
                Search = "contract",
                Status = "pending_signature",
                Page = 2,
            });

        var query = handler.Requests.Single().RequestUri!.Query;
        query.Should().Contain("signer-access-code=access");
        query.Should().Contain("search=contract");
        query.Should().Contain("status=pending_signature");
        query.Should().Contain("page=2");
    }

    [Fact]
    public async Task DownloadOverloads_UsePublicArtifactRouteWithoutAccessCodeOrUserCredentials()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(
            HttpMethod.Get,
            "/signers/signer-1/documents/doc-1/download/pades",
            "pdf");
        var resource = new SigningResource(
            FakeHttpMessageHandler.CreateClient(handler),
            request => request.Headers.Add("X-Api-Key", "must-not-leak"));

        var result = await resource.DownloadPublicAsync(
            "signer-1",
            "doc-1",
            artifactName: DocumentArtifactNames.Pades);
#pragma warning disable CS0618 // Compatibility overload must remain operational.
        var legacyResult = await resource.DownloadAsync(
            "signer-1",
            "doc-1",
            signerAccessCode: "ignored",
            artifactName: DocumentArtifactNames.Pades);
#pragma warning restore CS0618

        result.Should().NotBeEmpty();
        legacyResult.Should().Equal(result);
        handler.Requests.Should().HaveCount(2);
        handler.Requests.Should().OnlyContain(sent =>
            sent.RequestUri!.PathAndQuery == "/v1/signers/signer-1/documents/doc-1/download/pades" &&
            !sent.Headers.Contains("X-Api-Key"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SignerDocuments_AsWorkspace_SendWorkspaceCredentialAndNoAccessCode(bool search)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/signers/signer-1/documents",
            FakeHttpMessageHandler.ApiOk(new[] { new { id = "doc-1", name = "c", status = "pending_signature" } }));
        var resource = new SigningResource(
            FakeHttpMessageHandler.CreateClient(handler),
            request => request.Headers.Add("X-Api-Key", "key"));

        var parameters = new SignerDocumentListParams { Search = "c", Page = 1 };
        var result = search
            ? await resource.SearchDocumentsAsWorkspaceAsync("signer-1", parameters)
            : await resource.ListDocumentsAsWorkspaceAsync("signer-1", parameters);

        result.Data.Should().ContainSingle();
        var sent = handler.Requests.Single();
        sent.RequestUri!.AbsolutePath.Should().Be(search
            ? "/v1/signers/signer-1/documents/search"
            : "/v1/signers/signer-1/documents");
        sent.RequestUri.Query.Should().NotContain("signer-access-code");
        sent.Headers.Contains("X-Api-Key").Should().BeTrue();
    }

    [Theory]
    [InlineData(false, null)]
    [InlineData(false, "")]
    [InlineData(false, " ")]
    [InlineData(true, null)]
    [InlineData(true, "")]
    public async Task SignerDocuments_WithAccessCode_RejectMissingCodeInsteadOfUsingWorkspaceCredentials(bool search, string? code)
    {
        var handler = new FakeHttpMessageHandler();
        var resource = new SigningResource(
            FakeHttpMessageHandler.CreateClient(handler),
            request => request.Headers.Add("X-Api-Key", "key"));

        Func<Task> act = search
            ? () => resource.SearchDocumentsAsync("signer-1", code!)
            : () => resource.ListDocumentsAsync("signer-1", code!);

        await act.Should().ThrowAsync<Assinafy.Sdk.Exceptions.ValidationException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SignerDocuments_WithAccessCode_NeverSendWorkspaceCredential()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/signers/signer-1/documents",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));
        var resource = new SigningResource(
            FakeHttpMessageHandler.CreateClient(handler),
            request => request.Headers.Add("X-Api-Key", "key"));

        await resource.ListDocumentsAsync("signer-1", "access");
        await resource.SearchDocumentsAsync("signer-1", "access");

        handler.Requests.Should().HaveCount(2).And.OnlyContain(r =>
            !r.Headers.Contains("X-Api-Key") && r.RequestUri!.Query.Contains("signer-access-code=access"));
    }
}
