using System.Net;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class TransportContractTests
{
    [Theory]
    [InlineData("../users/api-keys", "..%2Fusers%2Fapi-keys")]
    [InlineData("doc?force=true", "doc%3Fforce%3Dtrue")]
    [InlineData("doc#fragment", "doc%23fragment")]
    public async Task DestructiveRoute_EncodesDocumentIdAsOnePathSegment(
        string documentId,
        string encodedDocumentId)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(
            HttpMethod.Delete,
            "/documents/",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));
        var resource = new DocumentResource(FakeHttpMessageHandler.CreateClient(handler), "acc");

        await resource.DeleteAsync(documentId);

        var uri = handler.Requests.Should().ContainSingle().Subject.RequestUri!;
        uri.AbsolutePath.Should().EndWith($"/documents/{encodedDocumentId}");
        uri.Query.Should().BeEmpty();
        uri.Fragment.Should().BeEmpty();
        uri.AbsolutePath.Should().NotBe("/v1/users/api-keys");
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public async Task DestructiveRoute_RejectsDotSegmentsBeforeSending(string documentId)
    {
        var handler = new FakeHttpMessageHandler();
        var resource = new DocumentResource(FakeHttpMessageHandler.CreateClient(handler), "acc");

        await ((Func<Task>)(() => resource.DeleteAsync(documentId)))
            .Should().ThrowAsync<ValidationException>();

        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task SuccessEnvelope_RequiresIntegerStatus()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/doc-1", new
        {
            status = "200",
            message = "",
            data = new { id = "doc-1", status = "metadata_ready" },
        });
        var resource = new DocumentResource(FakeHttpMessageHandler.CreateClient(handler), "acc");

        await ((Func<Task>)(() => resource.GetAsync("doc-1")))
            .Should().ThrowAsync<SerializationException>();
    }

    [Fact]
    public async Task SuccessEnvelope_RequiresStatus()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/doc-1", new
        {
            message = "",
            data = new { id = "doc-1", status = "metadata_ready" },
        });
        var resource = new DocumentResource(FakeHttpMessageHandler.CreateClient(handler), "acc");

        await ((Func<Task>)(() => resource.GetAsync("doc-1")))
            .Should().ThrowAsync<SerializationException>();
    }

    [Fact]
    public async Task ListBody_RequiresNonNullData()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/statuses",
            new { status = 200, message = "", data = (object?)null });
        var resource = new DocumentResource(FakeHttpMessageHandler.CreateClient(handler), "acc");

        await ((Func<Task>)(() => resource.ListStatusesAsync()))
            .Should().ThrowAsync<SerializationException>();
    }

    [Fact]
    public async Task PaginatedList_RequiresData()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/accounts/acc/documents",
            new { status = 200, message = "" });
        var resource = new DocumentResource(FakeHttpMessageHandler.CreateClient(handler), "acc");

        await ((Func<Task>)(() => resource.ListAsync()))
            .Should().ThrowAsync<SerializationException>();
    }

    [Fact]
    public async Task VoidSuccessEnvelope_MayOmitData()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Delete, "/documents/doc-1",
            new { status = 200, message = "Deleted" });
        var resource = new DocumentResource(FakeHttpMessageHandler.CreateClient(handler), "acc");

        await resource.DeleteAsync("doc-1");
    }

    [Fact]
    public async Task RequiredResponseData_CannotBeNull()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/doc-1",
            new { status = 200, message = "", data = (object?)null });

        var resource = new DocumentResource(FakeHttpMessageHandler.CreateClient(handler), "acc");

        await ((Func<Task>)(() => resource.GetAsync("doc-1")))
            .Should().ThrowAsync<SerializationException>();
    }

    [Fact]
    public async Task HttpFailure_WinsOverIncorrectEnvelopeStatusAndPreservesDetails()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(
            HttpMethod.Get,
            "/documents/doc-1",
            new
            {
                status = 200,
                message = "Deletion blocked",
                data = new { restrictions = new[] { "active documents" } },
            },
            HttpStatusCode.InternalServerError);

        var resource = new DocumentResource(FakeHttpMessageHandler.CreateClient(handler), "acc");
        var exception = (await ((Func<Task>)(() => resource.GetAsync("doc-1")))
            .Should().ThrowAsync<ApiException>()).Which;

        exception.StatusCode.Should().Be(500);
        exception.Details!.Value.GetProperty("restrictions")[0].GetString()
            .Should().Be("active documents");
    }

    [Fact]
    public async Task BinaryEndpoint_RecognizesJsonErrorEnvelopeOnHttpSuccess()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/doc-1/download/original",
            new { status = 404, message = "Artifact not found", data = (object?)null });

        var resource = new DocumentResource(FakeHttpMessageHandler.CreateClient(handler), "acc");
        var exception = (await ((Func<Task>)(() => resource.DownloadAsync("doc-1", "original")))
            .Should().ThrowAsync<ApiException>()).Which;

        exception.StatusCode.Should().Be(404);
    }
}
