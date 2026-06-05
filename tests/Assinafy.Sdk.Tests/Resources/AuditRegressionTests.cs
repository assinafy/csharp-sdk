using System.Net.Http;
using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

/// <summary>
/// Regression coverage added during the 1.2.0 audit: guards the removed/changed behaviour
/// (signer_ids removal, step support, webhook delete removal, signing-progress fallback) and
/// fills coverage gaps (resend URLs, api-keys CRUD, error paths, bulk-op guards).
/// </summary>
public sealed class AuditRegressionTests
{
    private static HttpClient Client(FakeHttpMessageHandler handler) =>
        FakeHttpMessageHandler.CreateClient(handler);

    // ---- Assignment payload: signer_ids removed (K4), step added (K2) ----

    [Fact]
    public void BuildPayload_DoesNotEmitRedundantSignerIds()
    {
        var body = AssignmentResource.BuildPayload(new CreateAssignmentRequest { SignerIds = ["a", "b"] });

        body.ContainsKey("signer_ids").Should().BeFalse();
        var signers = (List<Dictionary<string, object?>>)body["signers"]!;
        signers.Should().HaveCount(2);
        signers[0]["id"].Should().Be("a");
    }

    [Fact]
    public void BuildPayload_EmitsSigningOrderStep()
    {
        var body = AssignmentResource.BuildPayload(new CreateAssignmentRequest
        {
            Signers = [new SignerRef { Id = "a", Step = 1 }, new SignerRef { Id = "b", Step = 2 }],
        });

        var signers = (List<Dictionary<string, object?>>)body["signers"]!;
        signers[0]["step"].Should().Be(1);
        signers[1]["step"].Should().Be(2);
    }

    // ---- Resend / estimate-resend: URL + verb split ----

    [Fact]
    public async Task ResendNotification_PutsToSignerResendUrl()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/signers/sgn-1/resend",
            FakeHttpMessageHandler.ApiOk(new { is_sent = true, document_id = "doc-1", signer_id = "sgn-1" }));

        var resource = new AssignmentResource(Client(handler));
        var result = await resource.ResendNotificationAsync("doc-1", "asg-1", "sgn-1");

        result.IsSent.Should().BeTrue();
        var request = handler.Requests.Last();
        request.Method.Should().Be(HttpMethod.Put);
        request.RequestUri!.PathAndQuery.Should().EndWith("/documents/doc-1/assignments/asg-1/signers/sgn-1/resend");
    }

    [Fact]
    public async Task EstimateResendCost_PostsToEstimateUrl()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/estimate-resend-cost",
            FakeHttpMessageHandler.ApiOk(new { total = 0m, has_sufficient_credits = true }));

        var resource = new AssignmentResource(Client(handler));
        await resource.EstimateResendCostAsync("doc-1", "asg-1", "sgn-1");

        var request = handler.Requests.Last();
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.PathAndQuery.Should().EndWith("/signers/sgn-1/estimate-resend-cost");
    }

    // ---- Create from template: body shape incl. step (K2) ----

    [Fact]
    public async Task CreateFromTemplate_SerializesSignersOptionsAndStep()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/templates/tmpl-1/documents",
            FakeHttpMessageHandler.ApiOk(new
            {
                id = "doc-1",
                name = "x",
                status = "uploaded",
                created_at = "t",
                is_closed = false,
                pages = Array.Empty<object>(),
            }));

        var resource = new DocumentResource(Client(handler), "acc");
        await resource.CreateFromTemplateAsync(
            "tmpl-1",
            [new TemplateSigner { RoleId = "role-1", Id = "s1", Step = 1 }],
            new CreateDocumentFromTemplateOptions
            {
                Name = "Contract",
                Message = "please sign",
                EditorFields = [new TemplateEditorField { FieldId = "f1", Value = "v" }],
            });

        var body = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0)).RootElement;
        var signer = body.GetProperty("signers")[0];
        signer.GetProperty("role_id").GetString().Should().Be("role-1");
        signer.GetProperty("id").GetString().Should().Be("s1");
        signer.GetProperty("step").GetInt32().Should().Be(1);
        body.GetProperty("name").GetString().Should().Be("Contract");
        body.GetProperty("message").GetString().Should().Be("please sign");
        body.GetProperty("editor_fields")[0].GetProperty("field_id").GetString().Should().Be("f1");
    }

    // ---- Signing-progress / fully-signed fallback to per-signer flags ----

    [Fact]
    public async Task GetSigningProgress_FallsBackToSignerFlagsWhenSummaryAbsent()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/doc-1", FakeHttpMessageHandler.ApiOk(new
        {
            id = "doc-1",
            name = "x",
            status = "pending_signature",
            created_at = "t",
            is_closed = false,
            pages = Array.Empty<object>(),
            assignment = new
            {
                id = "a1",
                signers = new[]
                {
                    new { id = "s1", full_name = "A", completed = true },
                    new { id = "s2", full_name = "B", completed = false },
                },
            },
        }));

        var resource = new DocumentResource(Client(handler), "acc");
        var progress = await resource.GetSigningProgressAsync("doc-1");

        progress.Total.Should().Be(2);
        progress.Signed.Should().Be(1);
        progress.Pending.Should().Be(1);
        progress.Percentage.Should().Be(50);
    }

    // ---- Signing: access code on the sign URL + bulk-op empty guards ----

    [Fact]
    public async Task Sign_PutsAccessCodeOnUrl()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/documents/doc-1/assignments/asg-1",
            FakeHttpMessageHandler.ApiOk(new { }));

        var resource = new SigningResource(Client(handler));
        await resource.SignAsync("doc-1", "asg-1", "code-123",
            [new SignAssignmentValue { ItemId = "i", FieldId = "f", PageId = "p", Value = "v" }]);

        handler.Requests.Last().RequestUri!.PathAndQuery
            .Should().Contain("/documents/doc-1/assignments/asg-1?signer-access-code=code-123");
    }

    [Fact]
    public async Task SignMultiple_ThrowsOnEmptyList()
    {
        var resource = new SigningResource(Client(new FakeHttpMessageHandler()));
        var act = () => resource.SignMultipleAsync("code", Array.Empty<string>());
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task DeclineMultiple_ThrowsOnEmptyList()
    {
        var resource = new SigningResource(Client(new FakeHttpMessageHandler()));
        var act = () => resource.DeclineMultipleAsync("code", Array.Empty<string>(), "reason");
        await act.Should().ThrowAsync<ValidationException>();
    }

    // ---- Authentication: api-keys CRUD + reset-password token guard ----

    [Fact]
    public async Task ApiKeys_CrudHitDocumentedEndpoints()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/users/api-keys", FakeHttpMessageHandler.ApiOk(new { api_key = "new-key" }));
        handler.AddJsonResponse(HttpMethod.Get, "/users/api-keys", FakeHttpMessageHandler.ApiOk(new { api_key = "masked" }));
        handler.AddJsonResponse(HttpMethod.Delete, "/users/api-keys", FakeHttpMessageHandler.ApiOk(new { }));

        var resource = new AuthenticationResource(Client(handler));
        (await resource.CreateApiKeyAsync(new CreateApiKeyRequest { Password = "p" })).ApiKey.Should().Be("new-key");
        (await resource.GetApiKeyAsync()).ApiKey.Should().Be("masked");
        await resource.DeleteApiKeyAsync();

        handler.Requests.Select(r => r.Method).Should()
            .Contain(new[] { HttpMethod.Post, HttpMethod.Get, HttpMethod.Delete });
    }

    [Fact]
    public async Task ResetPassword_RequiresToken()
    {
        var resource = new AuthenticationResource(Client(new FakeHttpMessageHandler()));
        var act = () => resource.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = "user@example.com",
            NewPassword = "secret",
            // Token omitted — the runtime guard must still reject the call.
        });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    // ---- Webhooks: null events guard ----

    [Fact]
    public async Task UpdateSubscription_ThrowsWhenEventsNull()
    {
        var resource = new WebhookResource(Client(new FakeHttpMessageHandler()), "acc");
        var act = () => resource.UpdateSubscriptionAsync(new UpdateWebhookSubscriptionRequest
        {
            Events = null!,
            Url = "https://example.com",
            Email = "ops@example.com",
            IsActive = true,
        });

        await act.Should().ThrowAsync<ValidationException>();
    }

    // ---- Error envelope paths ----

    [Fact]
    public async Task ApiErrorEnvelope_ThrowsApiExceptionWithStatusAndMessage()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/documents/doc-x",
            "{\"status\":422,\"data\":null,\"message\":\"Boom\"}");

        var resource = new DocumentResource(Client(handler), "acc");
        var act = () => resource.GetAsync("doc-x");

        var ex = (await act.Should().ThrowAsync<ApiException>()).Which;
        ex.StatusCode.Should().Be(422);
        ex.ApiMessage.Should().Be("Boom");
    }

    [Fact]
    public async Task TransportFailure_SurfacesNetworkException()
    {
        var http = new HttpClient(new ThrowingHandler()) { BaseAddress = new Uri("http://test.api/v1/") };
        var resource = new DocumentResource(http, "acc");

        var act = () => resource.GetAsync("doc-1");
        await act.Should().ThrowAsync<NetworkException>();
    }

    // ---- FindByEmail pages past the first 100 results ----

    [Fact]
    public async Task FindByEmail_PagesUntilExactMatchFound()
    {
        var handler = new FakeHttpMessageHandler();
        var page2Headers = new Dictionary<string, string>
        {
            ["x-pagination-current-page"] = "2",
            ["x-pagination-page-count"] = "2",
        };
        var page1Headers = new Dictionary<string, string>
        {
            ["x-pagination-current-page"] = "1",
            ["x-pagination-page-count"] = "2",
        };
        handler.AddJsonResponse(HttpMethod.Get, "&page=2",
            FakeHttpMessageHandler.ApiOk(new[] { new { id = "s2", full_name = "Target", email = "target@x.com" } }),
            headers: page2Headers);
        handler.AddJsonResponse(HttpMethod.Get, "&page=1",
            FakeHttpMessageHandler.ApiOk(new[] { new { id = "s1", full_name = "Other", email = "other@x.com" } }),
            headers: page1Headers);

        var resource = new SignerResource(Client(handler), "acc");
        var found = await resource.FindByEmailAsync("target@x.com");

        found.Should().NotBeNull();
        found!.Id.Should().Be("s2");
    }

    // ---- FlexibleStringJsonConverter preserves exact numeric token text ----

    [Fact]
    public async Task NumberCoercedToString_PreservesExactTokenText()
    {
        var handler = new FakeHttpMessageHandler();
        // "name" is a string field receiving a JSON number in exponent form. The old
        // GetInt64/GetDecimal round-trip would have produced "1000"; exact text is "1e3".
        handler.AddRawResponse(HttpMethod.Get, "/documents/doc-1",
            "{\"status\":200,\"message\":\"\",\"data\":{\"id\":\"doc-1\",\"name\":1e3," +
            "\"status\":\"uploaded\",\"created_at\":\"t\",\"is_closed\":false,\"pages\":[]}}");

        var resource = new DocumentResource(Client(handler), "acc");
        var doc = await resource.GetAsync("doc-1");

        doc.Name.Should().Be("1e3");
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("simulated transport failure");
    }
}
