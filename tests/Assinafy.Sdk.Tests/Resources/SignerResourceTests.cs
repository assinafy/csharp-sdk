using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class SignerResourceTests
{
    private static SignerResource CreateResource(FakeHttpMessageHandler handler, string? accountId = "test-account")
        => new(FakeHttpMessageHandler.CreateClient(handler), accountId);

    [Fact]
    public async Task Create_ThrowsWhenNoAccountId()
    {
        var handler = new FakeHttpMessageHandler();
        var resource = CreateResource(handler, accountId: null);

        var act = () => resource.CreateAsync(new CreateSignerRequest { FullName = "Test", Email = "test@test.com" });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Create_ThrowsOnInvalidEmail()
    {
        var handler = new FakeHttpMessageHandler();
        var resource = CreateResource(handler);

        var act = () => resource.CreateAsync(new CreateSignerRequest { FullName = "Test", Email = "not-an-email" });

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*email*");
    }

    [Fact]
    public async Task Create_PostsDocumentedSignerShape()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/custom-account/signers",
            FakeHttpMessageHandler.ApiOk(new
            {
                id = "123",
                full_name = "Test",
                email = "test@test.com",
                whatsapp_phone_number = "+5548999990000",
            }));

        var resource = CreateResource(handler, "default-account");
        var result = await resource.CreateAsync(
            new CreateSignerRequest
            {
                FullName = "Test",
                Email = "test@test.com",
                WhatsAppPhoneNumber = "+5548999990000",
            },
            accountId: "custom-account");

        result.Id.Should().Be("123");
        var body = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0));
        body.RootElement.GetProperty("full_name").GetString().Should().Be("Test");
        body.RootElement.GetProperty("whatsapp_phone_number").GetString().Should().Be("+5548999990000");
    }

    [Fact]
    public async Task List_PassesSearchViaQueryParams()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "search=john%40example.com",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));

        var resource = CreateResource(handler);
        await resource.ListAsync(new Dictionary<string, string?> { ["search"] = "john@example.com" });

        handler.Requests.Should().Contain(r =>
            r.RequestUri!.Query.Contains("search=john%40example.com"));
    }

    [Fact]
    public async Task FindByEmail_ReturnsMatchingSigner()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/signers",
            FakeHttpMessageHandler.ApiOk(new[]
            {
                new { id = "1", full_name = "John", email = "JOHN@EXAMPLE.COM" },
            }));

        var resource = CreateResource(handler);
        var result = await resource.FindByEmailAsync("john@example.com");

        result.Should().NotBeNull();
        result!.Id.Should().Be("1");
    }

    [Fact]
    public async Task Update_ThrowsWhenNoSignerId()
    {
        var handler = new FakeHttpMessageHandler();
        var resource = CreateResource(handler);

        var act = () => resource.UpdateAsync("", new UpdateSignerRequest { FullName = "Test" });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task GetSelf_UsesSignerAccessCodeQuery()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "signer-access-code=access",
            FakeHttpMessageHandler.ApiOk(new { id = "signer-1", full_name = "Signer", has_signature = true }));

        var resource = CreateResource(handler);
        var result = await resource.GetSelfAsync("access");

        result.HasSignature.Should().BeTrue();
        handler.Requests.Should().Contain(r => r.RequestUri!.PathAndQuery.Contains("/signers/self"));
    }

    [Fact]
    public async Task ConfirmData_PutsDocumentSignerDataWithAccessCode()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/signers/confirm-data",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));

        var resource = CreateResource(handler);
        await resource.ConfirmDataAsync(
            "doc-1",
            "access",
            new ConfirmSignerDataRequest
            {
                Email = "john@example.com",
                HasAcceptedTerms = true,
            });

        handler.Requests.Should().Contain(r =>
            r.RequestUri!.PathAndQuery.Contains("/documents/doc-1/signers/confirm-data?signer-access-code=access"));
    }
}
