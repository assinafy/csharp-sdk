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
    public async Task Get_AddsSignerAccessCode()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "signer-access-code=access",
            FakeHttpMessageHandler.ApiOk(new { id = "doc-1", name = "contract.pdf", status = "pending_signature", created_at = "2026-01-01", updated_at = "2026-01-01" }));

        var resource = CreateResource(handler);
        var result = await resource.GetAsync("access");

        result.Id.Should().Be("doc-1");
        handler.Requests.Should().Contain(r => r.RequestUri!.PathAndQuery.Contains("/sign?signer-access-code=access"));
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
        body.RootElement[0].GetProperty("item_id").GetString().Should().Be("item");
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
    public async Task ListDocuments_AddsSignerAccessCodeAndFilters()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "status=pending_signature",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));

        var resource = CreateResource(handler);
        await resource.ListDocumentsAsync(
            "signer-1",
            "access",
            new SignerDocumentListParams { Status = "pending_signature" });

        handler.Requests.Should().Contain(r =>
            r.RequestUri!.PathAndQuery.Contains("/signers/signer-1/documents") &&
            r.RequestUri.Query.Contains("signer-access-code=access"));
    }
}
