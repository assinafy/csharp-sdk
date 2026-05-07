using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class PublicDocumentResourceTests
{
    private static PublicDocumentResource CreateResource(FakeHttpMessageHandler handler)
        => new(FakeHttpMessageHandler.CreateClient(handler));

    [Fact]
    public async Task Get_CallsPublicDocumentEndpoint()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/public/documents/doc-1",
            FakeHttpMessageHandler.ApiOk(new { id = "doc-1", name = "contract.pdf", page_count = "1", created_by = "John" }));

        var resource = CreateResource(handler);
        var result = await resource.GetAsync("doc-1");

        result.Name.Should().Be("contract.pdf");
    }

    [Fact]
    public async Task SendToken_PutsRecipientAndChannel()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/public/documents/doc-1/send-token",
            FakeHttpMessageHandler.ApiOk(new { channel = "email", recipient = "john@example.com" }));

        var resource = CreateResource(handler);
        var result = await resource.SendTokenAsync("doc-1", new SendDocumentTokenRequest
        {
            Recipient = "john@example.com",
            Channel = "email",
        });

        result.Channel.Should().Be("email");
    }
}
