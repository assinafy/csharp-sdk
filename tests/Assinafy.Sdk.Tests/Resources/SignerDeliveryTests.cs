using System.Net;
using System.Text;
using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class SignerDeliveryTests
{
    private static SigningResource Signing(FakeHttpMessageHandler handler) =>
        new(FakeHttpMessageHandler.CreateClient(handler), request => request.Headers.Add("X-Api-Key", "must-not-leak"));

    [Fact]
    public async Task DocumentNavigation_EncodesEnvelopeFiltersWithoutCredentials()
    {
        var handler = new FakeHttpMessageHandler();
        var document = new
        {
            id = "doc",
            name = "Contract",
            status = "certificated",
            is_signer_access_closed = true,
            current_signer = new { id = "signer", completed = true, verification_method = "Email", notification_methods = new[] { "Email" } },
            certified_copy = new { available = true, notification_method = "Email", resends_left = (int?)null },
        };
        handler.AddJsonResponse(HttpMethod.Get, "/sign?", FakeHttpMessageHandler.ApiOk(document));
        handler.AddJsonResponse(HttpMethod.Get, "/signers/signer/document?", FakeHttpMessageHandler.ApiOk(document));
        handler.AddJsonResponse(HttpMethod.Get, "/signers/signer/documents", FakeHttpMessageHandler.ApiOk(new[] { document }));
        var resource = Signing(handler);

        var result = await resource.GetWithOptionsAsync("code&secret", new SigningDocumentParams
        {
            DocumentId = "doc/2",
            ExpandEnvelope = true,
            HasAcceptedTerms = false,
        });
        result.IsSignerAccessClosed.Should().BeTrue();
        result.CertifiedCopy!.Available.Should().BeTrue();
        result.CertifiedCopy.ResendsLeft.Should().BeNull();
        await resource.GetCurrentDocumentWithEnvelopeAsync("signer", "code&secret");
        await resource.ListDocumentsAsync("signer", "code&secret", new SignerDocumentListParams { EnvelopeId = "envelope/1" });
        await resource.SearchDocumentsAsync("signer", "code&secret", new SignerDocumentListParams { EnvelopeId = "envelope/1" });

        handler.Requests[0].RequestUri!.PathAndQuery.Should().Be("/v1/sign?signer-access-code=code%26secret&has_accepted_terms=false&expand=envelope&document_id=doc%2F2");
        handler.Requests[1].RequestUri!.PathAndQuery.Should().Be("/v1/signers/signer/document?signer-access-code=code%26secret&expand=envelope");
        handler.Requests[2].RequestUri!.Query.Should().Contain("envelope_id=envelope%2F1");
        handler.Requests[3].RequestUri!.Query.Should().Contain("envelope_id=envelope%2F1");
        handler.Requests.Should().OnlyContain(r => !r.Headers.Contains("X-Api-Key"));
    }

    [Fact]
    public async Task BrandingAndCertifiedCopies_UseSignerCredentialOrDownloadTokenOnly()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/sign/theme?", FakeHttpMessageHandler.ApiOk(new { account_name = "Example", primary_color = "aabbcc", secondary_color = "ffffff", logo = (string?)null }));
        handler.AddRawResponse(HttpMethod.Get, "/sign/logo?", "image");
        handler.AddJsonResponse(HttpMethod.Post, "/sign/certified-copy?", FakeHttpMessageHandler.ApiOk(new { available = true, notification_method = "Email", resends_left = (int?)null, sent_to = "j***@example.com" }));
        handler.AddRawResponse(HttpMethod.Get, "/signers/downloads/token%2Fvalue", "pdf");
        handler.AddJsonResponse(HttpMethod.Post, "/signers/downloads/token%2Fvalue/resend", FakeHttpMessageHandler.ApiOk(new { available = false, notification_method = "Whatsapp", resends_left = 0, sent_to = "+55***00" }));
        handler.AddRawResponse(HttpMethod.Get, "/signers/signer%2F1/documents/doc%2F1/download/original?", "original");
        var resource = Signing(handler);

        (await resource.GetThemeAsync("code&secret")).AccountName.Should().Be("Example");
        Encoding.UTF8.GetString(await resource.DownloadLogoAsync("code&secret")).Should().Be("image");
        (await resource.SendCertifiedCopyAsync("code&secret", "doc-2")).SentTo.Should().Be("j***@example.com");
        Encoding.UTF8.GetString(await resource.DownloadCertifiedCopyAsync("token/value")).Should().Be("pdf");
        await resource.DownloadCertifiedCopyAsync("token/value", pades: true);
        (await resource.ResendCertifiedCopyAsync("token/value")).ResendsLeft.Should().Be(0);
        Encoding.UTF8.GetString(await resource.DownloadOriginalAsync("signer/1", "doc/1", "code&secret")).Should().Be("original");

        handler.Requests.Select(r => r.Method.Method).Should().Equal("GET", "GET", "POST", "GET", "GET", "POST", "GET");
        handler.Requests.Select(r => r.RequestUri!.PathAndQuery).Should().Equal(
            "/v1/sign/theme?signer-access-code=code%26secret", "/v1/sign/logo?signer-access-code=code%26secret",
            "/v1/sign/certified-copy?signer-access-code=code%26secret", "/v1/signers/downloads/token%2Fvalue",
            "/v1/signers/downloads/token%2Fvalue/pades", "/v1/signers/downloads/token%2Fvalue/resend",
            "/v1/signers/signer%2F1/documents/doc%2F1/download/original?signer-access-code=code%26secret");
        handler.Requests.Should().OnlyContain(r => !r.Headers.Contains("X-Api-Key") && r.Headers.Authorization == null);
        using var body = JsonDocument.Parse(handler.RequestBodies[2]);
        body.RootElement.EnumerateObject().Should().ContainSingle();
        body.RootElement.GetProperty("document_id").GetString().Should().Be("doc-2");
        handler.RequestBodies[5].Should().BeEmpty();
    }

    [Fact]
    public async Task Confirmation_AcceptsEmptyDataWithoutASecondRequest()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/confirm-data?", FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));
        var resource = new SignerResource(FakeHttpMessageHandler.CreateClient(handler), "account", request => request.Headers.Add("X-Api-Key", "must-not-leak"));
        await resource.ConfirmDataAsync("doc/1", "code&secret", new ConfirmSignerDataRequest
        {
            Email = "signer@example.com",
            WhatsAppPhoneNumber = "+5548999990000",
            HasAcceptedTerms = true,
        });
        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Method.Should().Be(HttpMethod.Put);
        handler.Requests[0].RequestUri!.PathAndQuery.Should().Be("/v1/documents/doc%2F1/signers/confirm-data?signer-access-code=code%26secret");
        handler.Requests[0].Headers.Contains("X-Api-Key").Should().BeFalse();
        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        body.RootElement.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("email", "whatsapp_phone_number", "has_accepted_terms");
    }

    [Fact]
    public async Task ConfirmationWithResult_ReadsSelfOnlyAfterSuccessfulConfirmation()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/confirm-data?", new { status = 400, message = "Contact mismatch" }, HttpStatusCode.BadRequest);
        var resource = new SignerResource(FakeHttpMessageHandler.CreateClient(handler));
        await ((Func<Task>)(() => resource.ConfirmDataWithResultAsync("doc", "code", new ConfirmSignerDataRequest { Email = "signer@example.com" })))
            .Should().ThrowAsync<ApiException>();
        handler.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public async Task NewSignerEndpoints_RejectMissingCredentialsBeforeTransport(string? code)
    {
        var handler = new FakeHttpMessageHandler();
        var resource = Signing(handler);
        Func<Task>[] calls = [
            () => resource.GetThemeAsync(code!), () => resource.DownloadLogoAsync(code!),
            () => resource.SendCertifiedCopyAsync(code!), () => resource.DownloadOriginalAsync("signer", "doc", code!),
            () => resource.DownloadCertifiedCopyAsync(code!), () => resource.ResendCertifiedCopyAsync(code!),
            () => resource.GetWithOptionsAsync(code!, new SigningDocumentParams()),
            () => resource.GetCurrentDocumentWithEnvelopeAsync("signer", code!),
        ];
        foreach (var call in calls) await call.Should().ThrowAsync<ValidationException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ExpiredDownloadAndResendLimit_AreSurfacedWithoutRetries()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/signers/downloads/", "", HttpStatusCode.Found);
        handler.AddJsonResponse(HttpMethod.Post, "/resend", new { status = 429, message = "Wait" }, HttpStatusCode.TooManyRequests);
        var resource = Signing(handler);
        (await ((Func<Task>)(() => resource.DownloadCertifiedCopyAsync("token"))).Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(302);
        (await ((Func<Task>)(() => resource.ResendCertifiedCopyAsync("token"))).Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(429);
        handler.Requests.Should().HaveCount(2);
    }
}
