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
#pragma warning disable CS0618
        var result = await resource.GetAsync("doc-1");
#pragma warning restore CS0618

        result.Name.Should().Be("contract.pdf");
    }

    [Fact]
    public async Task GetDetails_ReturnsFullDocumentWithoutUserCredentials()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/public/documents/doc-1",
            FakeHttpMessageHandler.ApiOk(new
            {
                id = "doc-1",
                account_id = "account-1",
                name = "contract.pdf",
                status = "pending_signature",
                tags = new[] { new { id = "tag-1", name = "Legal" } },
                pages = new[] { new { id = "page-1", number = 1, height = 1000, width = 800 } },
                created_at = "2026-08-19T00:00:00Z",
                updated_at = "2026-08-19T00:00:00Z",
            }));

        var resource = new PublicDocumentResource(
            FakeHttpMessageHandler.CreateClient(handler),
            request => request.Headers.Add("X-Api-Key", "must-not-leak"));
        var result = await resource.GetDetailsAsync("doc-1");

        result.AccountId.Should().Be("account-1");
        result.Status.Should().Be("pending_signature");
        result.Tags.Should().ContainSingle();
        result.Pages.Should().ContainSingle();
        handler.Requests.Single().Headers.Contains("X-Api-Key").Should().BeFalse();
    }

    [Fact]
    public async Task SendToken_PutsOptionalEmailAndAcceptsEnvelopeWithoutData()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/public/documents/doc-1/send-token",
            new { status = 200, message = "Token sent" });

        var resource = new PublicDocumentResource(
            FakeHttpMessageHandler.CreateClient(handler),
            request => request.Headers.Add("X-Api-Key", "must-not-leak"));
        await resource.SendTokenAsync("doc-1", "john@example.com");

        var body = System.Text.Json.JsonDocument.Parse(handler.RequestBodies.Single());
        body.RootElement.GetProperty("email").GetString().Should().Be("john@example.com");
        body.RootElement.EnumerateObject().Should().ContainSingle();
        handler.Requests.Single().Headers.Contains("X-Api-Key").Should().BeFalse();
    }

    [Fact]
    public async Task SendToken_OmitsBodyWhenEmailIsNotProvided()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/public/documents/doc-1/send-token",
            new { status = 200, message = "Token sent" });

        await CreateResource(handler).SendTokenAsync("doc-1");

        handler.RequestBodies.Single().Should().BeEmpty();
    }

    [Fact]
    public async Task LegacySendToken_PreservesWhatsappPayload()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/public/documents/doc-1/send-token",
            new { status = 200, message = "Token sent" });

#pragma warning disable CS0618
        var result = await CreateResource(handler).SendTokenAsync("doc-1", new SendDocumentTokenRequest
        {
            Recipient = "+5548999990000",
            Channel = SignerChannels.Whatsapp,
        });
#pragma warning restore CS0618

        var body = System.Text.Json.JsonDocument.Parse(handler.RequestBodies.Single());
        body.RootElement.GetProperty("recipient").GetString().Should().Be("+5548999990000");
        body.RootElement.GetProperty("channel").GetString().Should().Be(SignerChannels.Whatsapp);
        result.Channel.Should().Be(SignerChannels.Whatsapp);
    }
}
