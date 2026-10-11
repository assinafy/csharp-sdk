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

    private static object SendTokenData() => FakeHttpMessageHandler.ApiOk(new
    {
        document = new { resource = "document", id = "doc-1", name = "Contract.pdf", page_count = 3, created_by = "Jane Doe" },
        channel = "email",
        recipient = "signer@example.com",
    });

    [Fact]
    public async Task SendToken_PutsChannelAndRecipientWithoutCredentialsAndReturnsData()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/public/documents/doc-1/send-token", SendTokenData());

        var resource = new PublicDocumentResource(
            FakeHttpMessageHandler.CreateClient(handler),
            request => request.Headers.Add("X-Api-Key", "must-not-leak"));
        var result = await resource.SendTokenAsync("doc-1", new SendDocumentTokenRequest
        {
            Channel = SignerChannels.Email,
            Recipient = "signer@example.com",
        });

        var sent = handler.Requests.Single();
        sent.Method.Should().Be(HttpMethod.Put);
        sent.RequestUri!.AbsolutePath.Should().Be("/v1/public/documents/doc-1/send-token");
        sent.Headers.Contains("X-Api-Key").Should().BeFalse();
        using var body = System.Text.Json.JsonDocument.Parse(handler.RequestBodies.Single());
        body.RootElement.GetRawText().Should().Be("""{"channel":"email","recipient":"signer@example.com"}""");
        result.Channel.Should().Be(SendDocumentTokenChannels.Email);
        result.Recipient.Should().Be("signer@example.com");
        result.Document!.Id.Should().Be("doc-1");
        result.Document.PageCount.Should().Be("3");
        result.Document.CreatedBy.Should().Be("Jane Doe");
    }

    [Fact]
    public async Task SendToken_EmailOverloadSendsTheSameBody()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/public/documents/doc-1/send-token", SendTokenData());

        await CreateResource(handler).SendTokenAsync("doc-1", "signer@example.com");

        handler.RequestBodies.Single().Should().Be("""{"channel":"email","recipient":"signer@example.com"}""");
    }

    [Fact]
    public async Task SendToken_RejectsMissingRecipientAndUnsupportedChannelBeforeSending()
    {
        var handler = new FakeHttpMessageHandler();
        var resource = CreateResource(handler);

        await ((Func<Task>)(() => resource.SendTokenAsync("doc-1"))).Should().ThrowAsync<ArgumentException>();
        await ((Func<Task>)(() => resource.SendTokenAsync("doc-1", new SendDocumentTokenRequest
        {
            Channel = SignerChannels.Whatsapp,
            Recipient = "+5548999990000",
        }))).Should().ThrowAsync<Assinafy.Sdk.Exceptions.ValidationException>();
        handler.Requests.Should().BeEmpty();
    }
}
