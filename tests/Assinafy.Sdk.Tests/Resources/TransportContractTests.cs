using System.Net;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class TransportContractTests
{
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
