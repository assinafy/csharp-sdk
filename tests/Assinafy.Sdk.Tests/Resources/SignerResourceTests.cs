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

        var act = () => resource.CreateAsync(new CreateSignerRequest { FullName = "Test", Email = "test@example.com" });

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
    public async Task WriteMethods_RejectWhitespaceEmailBeforeTransport()
    {
        var handler = new FakeHttpMessageHandler();
        var resource = CreateResource(handler);

        await ((Func<Task>)(() => resource.CreateAsync(
                new CreateSignerRequest { FullName = "Test", Email = " " })))
            .Should().ThrowAsync<ValidationException>().WithMessage("*email*");
        await ((Func<Task>)(() => resource.UpdateAsync(
                "signer-1",
                new UpdateSignerRequest { Email = " " })))
            .Should().ThrowAsync<ValidationException>().WithMessage("*email*");
        await ((Func<Task>)(() => resource.ConfirmDataAsync(
                "doc-1",
                "access",
                new ConfirmSignerDataRequest { Email = " " })))
            .Should().ThrowAsync<ValidationException>().WithMessage("*email*");

        handler.Requests.Should().BeEmpty();
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
                email = "test@example.com",
                whatsapp_phone_number = "+5548999990000",
            }));

        var resource = CreateResource(handler, "default-account");
        var result = await resource.CreateAsync(
            new CreateSignerRequest
            {
                FullName = "Test",
                Email = "test@example.com",
                WhatsAppPhoneNumber = "+5548999990000",
            },
            accountId: "custom-account");

        result.Id.Should().Be("123");
        var body = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0));
        body.RootElement.GetProperty("full_name").GetString().Should().Be("Test");
        body.RootElement.GetProperty("whatsapp_phone_number").GetString().Should().Be("+5548999990000");
        body.RootElement.TryGetProperty("whats_app_phone_number", out _).Should().BeFalse(
            "WhatsAppPhoneNumber must serialize as 'whatsapp_phone_number' per the Assinafy API");
    }

    [Fact]
    public async Task Update_PostsWhatsappPhoneNumberSnakeCase()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/accounts/test-account/signers/s1",
            FakeHttpMessageHandler.ApiOk(new { id = "s1", full_name = "Updated" }));

        var resource = CreateResource(handler);
        await resource.UpdateAsync("s1", new UpdateSignerRequest
        {
            FullName = "Updated",
            GovernmentId = "12345678900",
            WhatsAppPhoneNumber = "+5548999990000",
        });

        var body = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0));
        body.RootElement.GetProperty("government_id").GetString().Should().Be("12345678900");
        body.RootElement.GetProperty("whatsapp_phone_number").GetString().Should().Be("+5548999990000");
    }

    [Fact]
    public async Task ConfirmData_PutsDocumentedFieldsAndReturnsUpdatedSigner()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/signers/confirm-data", FakeHttpMessageHandler.ApiOk(new
        {
            id = "signer-1",
            full_name = "John Doe",
            email = "john@example.com",
            government_id = "12345678900",
        }));

        var resource = CreateResource(handler);
        var result = await resource.ConfirmDataWithResultAsync("doc-1", "access", new ConfirmSignerDataRequest
        {
            FullName = "John Doe",
            Email = "john@example.com",
            GovernmentId = "12345678900",
            WhatsAppPhoneNumber = "+5548999990000",
            HasAcceptedTerms = true,
        });

        result.GovernmentId.Should().Be("12345678900");
        var body = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0));
        body.RootElement.GetProperty("full_name").GetString().Should().Be("John Doe");
        body.RootElement.GetProperty("email").GetString().Should().Be("john@example.com");
        body.RootElement.GetProperty("government_id").GetString().Should().Be("12345678900");
        body.RootElement.GetProperty("whatsapp_phone_number").GetString()
            .Should().Be("+5548999990000");
        body.RootElement.GetProperty("has_accepted_terms").GetBoolean().Should().BeTrue();
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
    public async Task Verify_UsesGenericOtpPayloadWithoutAccountCredentials()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/verify?signer-access-code=access",
            new { status = 200, message = "Verified" });
        var resource = new SignerResource(
            FakeHttpMessageHandler.CreateClient(handler),
            "test-account",
            request => request.Headers.Add("X-Api-Key", "must-not-leak"));

        await resource.VerifyAsync("access", "123456");

        var sent = handler.Requests.Should().ContainSingle().Subject;
        sent.Headers.Contains("X-Api-Key").Should().BeFalse();
        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        body.RootElement.GetProperty("verification-code").GetString().Should().Be("123456");
    }

    [Fact]
    public async Task ConfirmData_PutsDocumentSignerDataWithAccessCode()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/signers/confirm-data",
            FakeHttpMessageHandler.ApiOk(new { id = "signer-1", full_name = "John" }));

        var resource = new SignerResource(
            FakeHttpMessageHandler.CreateClient(handler),
            "test-account",
            request => request.Headers.Add("X-Api-Key", "must-not-leak"));
        await resource.ConfirmDataAsync(
            "doc-1",
            "access",
            new ConfirmSignerDataRequest
            {
                Email = "john@example.com",
            });

        var sent = handler.Requests.Single();
        sent.RequestUri!.PathAndQuery.Should().Be(
            "/v1/documents/doc-1/signers/confirm-data?signer-access-code=access");
        sent.Headers.Contains("X-Api-Key").Should().BeFalse();
    }
}
