using System.Net;
using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

/// <summary>
/// Tests for the endpoints and model fields added in v1.3.0,
/// plus previously-untested public methods.
/// </summary>
public sealed class AuditAdditionsTests
{
    private static HttpClient Client(FakeHttpMessageHandler handler) => FakeHttpMessageHandler.CreateClient(handler);

    // ---- New coverage-gap endpoints -------------------------------------------------

    [Fact]
    public async Task Assignments_List_UsesDocumentedPaginationOnly()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/assignments",
            FakeHttpMessageHandler.ApiOk(new[] { new { id = "as1", method = "virtual" } }));

        var resource = new AssignmentResource(Client(handler), "acc");
        var result = await resource.ListAsync(new AssignmentListParams { Page = 2, PerPage = 5 });

        result.Data.Should().ContainSingle().Which.Id.Should().Be("as1");
        var query = handler.Requests.Single(r => r.RequestUri!.AbsolutePath.EndsWith("/assignments")).RequestUri!.Query;
        query.Should().NotContain("accountId");
        query.Should().Contain("page=2");
        query.Should().Contain("per-page=5");
    }

    [Fact]
    public async Task Documents_Rename_PatchesNameToDocumentPath()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Patch, "/documents/doc-1",
            FakeHttpMessageHandler.ApiOk(new { id = "doc-1", name = "renamed", status = "metadata_ready" }));

        var resource = new DocumentResource(Client(handler), "acc");
        var result = await resource.RenameAsync("doc-1", "renamed");

        result.Name.Should().Be("renamed");
        var request = handler.Requests.Single(r => r.Method == HttpMethod.Patch);
        request.RequestUri!.AbsolutePath.Should().EndWith("/documents/doc-1");
        var body = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0));
        body.RootElement.GetProperty("name").GetString().Should().Be("renamed");
    }

    [Fact]
    public async Task Documents_Rename_RequiresName()
    {
        var handler = new FakeHttpMessageHandler();
        var resource = new DocumentResource(Client(handler), "acc");
        await ((Func<Task>)(() => resource.RenameAsync("doc-1", "")))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Documents_Search_HitsSearchRouteWithQuery()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/accounts/acc/documents/search",
            FakeHttpMessageHandler.ApiOk(new[] { new { id = "doc-1", name = "contract", status = "metadata_ready" } }));

        var resource = new DocumentResource(Client(handler), "acc");
        var result = await resource.SearchAsync("contract", status: "metadata_ready", page: 1, perPage: 10);

        result.Data.Should().ContainSingle().Which.Name.Should().Be("contract");
        var uri = handler.Requests.Single(r => r.RequestUri!.AbsolutePath.Contains("/documents/search")).RequestUri!;
        uri.Query.Should().Contain("search=contract");
        uri.Query.Should().Contain("status=metadata_ready");
    }

    [Fact]
    public async Task Signing_SearchDocuments_HitsSignerSearchRouteWithAccessCode()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/signers/sig-1/documents/search",
            FakeHttpMessageHandler.ApiOk(new[] { new { id = "doc-1", name = "c", status = "pending_signature" } }));

        var resource = new SigningResource(Client(handler));
        var result = await resource.SearchDocumentsAsync("sig-1", "code-1",
            new SignerDocumentListParams { Search = "c" });

        result.Data.Should().ContainSingle();
        var uri = handler.Requests.Single(r => r.RequestUri!.AbsolutePath.Contains("/documents/search")).RequestUri!;
        uri.Query.Should().Contain("signer-access-code=code-1");
        uri.Query.Should().Contain("search=c");
    }

    [Fact]
    public async Task Authentication_LinkSocialLogin_PostsProviderAndToken()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/auth/link-social-login",
            FakeHttpMessageHandler.ApiOk(new { }));

        var resource = new AuthenticationResource(Client(handler));
        await resource.LinkSocialLoginAsync(new LinkSocialLoginRequest { Provider = "google", Token = "tok" });

        var request = handler.Requests.Single(r => r.Method == HttpMethod.Post);
        request.RequestUri!.AbsolutePath.Should().EndWith("/auth/link-social-login");
        var body = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0));
        body.RootElement.GetProperty("provider").GetString().Should().Be("google");
        body.RootElement.GetProperty("token").GetString().Should().Be("tok");
    }

    [Fact]
    public async Task Authentication_LinkSocialLogin_RequiresProviderAndToken()
    {
        var handler = new FakeHttpMessageHandler();
        var resource = new AuthenticationResource(Client(handler));
        await ((Func<Task>)(() => resource.LinkSocialLoginAsync(new LinkSocialLoginRequest { Provider = "", Token = "t" })))
            .Should().ThrowAsync<ArgumentException>();
    }

    // ---- Previously-untested methods ------------------------------------------------

    [Fact]
    public async Task Documents_Thumbnail_DownloadsBinary()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/documents/doc-1/thumbnail", "PNG");
        var resource = new DocumentResource(Client(handler), "acc");

        (await resource.ThumbnailAsync("doc-1")).Should().NotBeEmpty();
        handler.Requests.Should().Contain(r => r.RequestUri!.AbsolutePath.EndsWith("/documents/doc-1/thumbnail"));
    }

    [Fact]
    public async Task Documents_DownloadPage_DownloadsBinary()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/documents/doc-1/pages/p1/download", "PAGE");
        var resource = new DocumentResource(Client(handler), "acc");

        (await resource.DownloadPageAsync("doc-1", "p1")).Should().NotBeEmpty();
        handler.Requests.Should().Contain(r => r.RequestUri!.AbsolutePath.EndsWith("/documents/doc-1/pages/p1/download"));
    }

    [Fact]
    public async Task Documents_IsFullySigned_TrueWhenCertificated()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/doc-1",
            FakeHttpMessageHandler.ApiOk(new { id = "doc-1", name = "c", status = "certificated" }));
        var resource = new DocumentResource(Client(handler), "acc");

        (await resource.IsFullySignedAsync("doc-1")).Should().BeTrue();
    }

    [Fact]
    public async Task Fields_ValidateMultiple_PostsValuesToValidateMultiplePath()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/fields/validate-multiple",
            FakeHttpMessageHandler.ApiOk(new[] { new { field_id = "f1", type = "cpf", success = true } }));
        var resource = new FieldResource(Client(handler), "acc");

        var result = await resource.ValidateMultipleAsync(
            [new ValidateFieldValueItem { FieldId = "f1", Value = "123" }]);

        result.Should().ContainSingle().Which.Success.Should().BeTrue();
        handler.Requests.Should().Contain(r => r.Method == HttpMethod.Post &&
            r.RequestUri!.AbsolutePath.EndsWith("/accounts/acc/fields/validate-multiple"));
    }

    [Fact]
    public async Task Signing_GetCurrentDocument_UsesAccessCode()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/signers/sig-1/document",
            FakeHttpMessageHandler.ApiOk(new { id = "doc-1", name = "c", status = "pending_signature" }));
        var resource = new SigningResource(Client(handler));

        var result = await resource.GetCurrentDocumentAsync("sig-1", "code-1");

        result.Id.Should().Be("doc-1");
        handler.Requests.Single().RequestUri!.Query.Should().Contain("signer-access-code=code-1");
    }

    [Fact]
    public async Task Authentication_RequestPasswordReset_PutsEmail()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/authentication/request-password-reset",
            FakeHttpMessageHandler.ApiOk(new { email = "user@example.com" }));
        var resource = new AuthenticationResource(Client(handler));

        var result = await resource.RequestPasswordResetAsync(new RequestPasswordResetRequest { Email = "user@example.com" });

        result.Email.Should().Be("user@example.com");
        handler.Requests.Should().Contain(r => r.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task Webhooks_Inactivate_PutsToInactivatePath()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/accounts/acc/webhooks/inactivate",
            FakeHttpMessageHandler.ApiOk(new { is_active = false, events = Array.Empty<string>() }));
        var resource = new WebhookResource(Client(handler), "acc");

        var result = await resource.InactivateAsync();

        result.IsActive.Should().BeFalse();
        handler.Requests.Should().Contain(r => r.Method == HttpMethod.Put &&
            r.RequestUri!.AbsolutePath.EndsWith("/accounts/acc/webhooks/inactivate"));
    }

    [Fact]
    public async Task Signers_AcceptTerms_PutsWithQueryCodeAndNoBody()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/signers/accept-terms",
            new { status = 200, message = "Terms accepted" });
        var resource = new SignerResource(
            Client(handler),
            "acc",
            request => request.Headers.Add("X-Api-Key", "must-not-leak"));

        var result = await resource.AcceptTermsAsync("code-1");

        result.HasAcceptedTerms.Should().BeTrue();
        handler.Requests.Should().ContainSingle();
        handler.Requests.First().RequestUri!.PathAndQuery.Should().Be(
            "/v1/signers/accept-terms?signer-access-code=code-1");
        handler.Requests.Should().OnlyContain(request => !request.Headers.Contains("X-Api-Key"));
        handler.RequestBodies.First().Should().BeEmpty();
    }

    [Fact]
    public async Task Signers_VerifyEmail_UsesQueryAccessCodeAndBodyVerificationCode()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/verify",
            new { status = 200, message = "Email verified" });
        var resource = new SignerResource(
            Client(handler),
            "acc",
            request => request.Headers.Add("X-Api-Key", "must-not-leak"));

        var result = await resource.VerifyEmailAsync("code-1", "123456");

        result.IsEmailVerified.Should().BeTrue();
        result.Email.Should().BeNull();
        handler.Requests.Should().ContainSingle();
        handler.Requests.First().RequestUri!.PathAndQuery.Should().Be(
            "/v1/verify?signer-access-code=code-1");
        handler.Requests.Should().OnlyContain(request => !request.Headers.Contains("X-Api-Key"));
        var body = JsonDocument.Parse(handler.RequestBodies.First(value => value.Length > 0));
        body.RootElement.GetProperty("verification-code").GetString().Should().Be("123456");
        body.RootElement.TryGetProperty("signer-access-code", out _).Should().BeFalse();
    }

    // ---- Model fidelity (silent data-loss fixes) -----------------------------------

    [Fact]
    public async Task Signer_DeserializesIsSignatureReusable()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/accounts/acc/signers/sig-1",
            FakeHttpMessageHandler.ApiOk(new
            {
                id = "sig-1",
                full_name = "A",
                has_signature = true,
                has_initial = false,
                is_signature_reusable = false,
            }));
        var resource = new SignerResource(Client(handler), "acc");

        var signer = await resource.GetAsync("sig-1");

        signer.HasSignature.Should().BeTrue();
        signer.IsSignatureReusable.Should().BeFalse();
    }

    [Fact]
    public async Task Template_DeserializesTagsAndDefaultDocumentTags()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/accounts/acc/templates/tpl-1",
            FakeHttpMessageHandler.ApiOk(new
            {
                id = "tpl-1",
                name = "NDA",
                status = "ready",
                tags = new[] { new { id = "t1", name = "legal" } },
                default_document_tags = new[] { new { id = "t2", name = "auto" } },
            }));
        var resource = new TemplateResource(Client(handler), "acc");

        var template = await resource.GetAsync("tpl-1");

        template.Tags.Should().ContainSingle().Which.Name.Should().Be("legal");
        template.DefaultDocumentTags.Should().ContainSingle().Which.Name.Should().Be("auto");
    }

    [Fact]
    public async Task Assignment_DeserializesTypedNotificationHistory()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/assignments",
            FakeHttpMessageHandler.ApiOk(new[]
            {
                new
                {
                    id = "as1",
                    method = "virtual",
                    signers = new[]
                    {
                        new
                        {
                            id = "sig-1",
                            full_name = "A",
                            notification_history = new[]
                            {
                                new { @event = "signature_request", status = "sent", sent_at = "2026-01-01T00:00:00Z" },
                            },
                        },
                    },
                },
            }));
        var resource = new AssignmentResource(Client(handler), "acc");

        var result = await resource.ListAsync();

        var history = result.Data.Single().Signers.Single().NotificationHistory;
        history.Should().ContainSingle();
        history![0].Status.Should().Be("sent");
        history[0].Event.Should().Be("signature_request");
    }

    // ---- Robustness: JsonException stays inside the AssinafyException hierarchy -----

    [Fact]
    public async Task NonJsonSuccessBody_ThrowsSerializationException()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/documents/doc-1", "<html>not json</html>");
        var resource = new DocumentResource(Client(handler), "acc");

        await ((Func<Task>)(() => resource.GetAsync("doc-1")))
            .Should().ThrowAsync<SerializationException>();
    }

    [Fact]
    public async Task NonJsonErrorBody_ThrowsApiException()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/documents/doc-1",
            "<html>502 Bad Gateway</html>", HttpStatusCode.BadGateway);
        var resource = new DocumentResource(Client(handler), "acc");

        var ex = await ((Func<Task>)(() => resource.GetAsync("doc-1")))
            .Should().ThrowAsync<ApiException>();
        ex.Which.StatusCode.Should().Be(502);
    }
}
