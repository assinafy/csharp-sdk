using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using Assinafy.Sdk.Webhooks;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

/// <summary>Webhook endpoints, signing secrets, signature verification, and two-factor authentication.</summary>
public sealed class WebhookEndpointAndMfaTests
{
    private static readonly object Endpoint = new
    {
        id = "ep1",
        name = "ERP",
        url = "https://example.com/hook",
        email = "ops@example.com",
        events = new[] { "document_ready" },
        is_active = true,
        signing_enabled = true,
        created_at = "2026-10-01T12:00:00Z",
        updated_at = "2026-10-01T12:00:00Z",
    };

    private static HttpClient Client(FakeHttpMessageHandler handler) => FakeHttpMessageHandler.CreateClient(handler);

    private static JsonElement LastBody(FakeHttpMessageHandler handler) =>
        JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0)).RootElement;

    private static (HttpMethod Method, string Path) Sent(FakeHttpMessageHandler handler) =>
        (handler.Requests.Single().Method, handler.Requests.Single().RequestUri!.AbsolutePath);

    // ---- Webhook endpoints -----------------------------------------------------------

    [Fact]
    public async Task ListEndpoints_GetsEndpointCollection()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/webhooks/endpoints", FakeHttpMessageHandler.ApiOk(new[] { Endpoint }));

        var result = await new WebhookResource(Client(handler), "acc").ListEndpointsAsync();

        Sent(handler).Should().Be((HttpMethod.Get, "/v1/accounts/acc/webhooks/endpoints"));
        var endpoint = result.Should().ContainSingle().Subject;
        endpoint.Id.Should().Be("ep1");
        endpoint.Name.Should().Be("ERP");
        endpoint.SigningEnabled.Should().BeTrue();
        endpoint.Events.Should().Equal("document_ready");
    }

    [Fact]
    public async Task CreateEndpoint_PostsSnakeCaseBodyAndOmitsUnsetFields()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/webhooks/endpoints", FakeHttpMessageHandler.ApiOk(Endpoint));

        await new WebhookResource(Client(handler), "acc").CreateEndpointAsync(new CreateWebhookEndpointRequest
        {
            Url = "https://example.com/hook",
            Email = "ops@example.com",
            Events = ["document_ready"],
            SigningEnabled = true,
        });

        Sent(handler).Should().Be((HttpMethod.Post, "/v1/accounts/acc/webhooks/endpoints"));
        var body = LastBody(handler);
        body.GetProperty("url").GetString().Should().Be("https://example.com/hook");
        body.GetProperty("email").GetString().Should().Be("ops@example.com");
        body.GetProperty("signing_enabled").GetBoolean().Should().BeTrue();
        body.TryGetProperty("is_active", out _).Should().BeFalse();
        body.TryGetProperty("name", out _).Should().BeFalse();
    }

    [Fact]
    public async Task CreateEndpoint_RequiresEvents()
    {
        var resource = new WebhookResource(Client(new FakeHttpMessageHandler()), "acc");
        var act = () => resource.CreateEndpointAsync(new CreateWebhookEndpointRequest
        {
            Url = "https://example.com/hook",
            Email = "ops@example.com",
            Events = [],
        });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task UpdateEndpoint_PutsOnlySentFields()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/webhooks/endpoints/ep1", FakeHttpMessageHandler.ApiOk(Endpoint));

        await new WebhookResource(Client(handler), "acc")
            .UpdateEndpointAsync("ep1", new UpdateWebhookEndpointRequest { IsActive = false });

        Sent(handler).Should().Be((HttpMethod.Put, "/v1/accounts/acc/webhooks/endpoints/ep1"));
        LastBody(handler).EnumerateObject().Select(p => p.Name).Should().Equal("is_active");
    }

    [Fact]
    public async Task GetAndDeleteEndpoint_UseEscapedEndpointPath()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/webhooks/endpoints/ep%201", FakeHttpMessageHandler.ApiOk(Endpoint));
        handler.AddJsonResponse(HttpMethod.Delete, "/webhooks/endpoints/ep1", FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));
        var resource = new WebhookResource(Client(handler), "acc");

        (await resource.GetEndpointAsync("ep 1")).Url.Should().Be("https://example.com/hook");
        await resource.DeleteEndpointAsync("ep1");

        handler.Requests.Select(r => (r.Method, r.RequestUri!.AbsolutePath)).Should().Equal(
            (HttpMethod.Get, "/v1/accounts/acc/webhooks/endpoints/ep%201"),
            (HttpMethod.Delete, "/v1/accounts/acc/webhooks/endpoints/ep1"));
    }

    [Fact]
    public async Task EndpointMethods_RejectDotSegments()
    {
        var resource = new WebhookResource(Client(new FakeHttpMessageHandler()), "acc");
        await ((Func<Task>)(() => resource.GetEndpointSecretAsync(".."))).Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task EndpointSecret_GetAndRotateHitDocumentedRoutes()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/ep1/secret", FakeHttpMessageHandler.ApiOk(new { secret = "whsec_old" }));
        handler.AddJsonResponse(HttpMethod.Post, "/ep1/secret/rotate", FakeHttpMessageHandler.ApiOk(new { secret = "whsec_new" }));
        var resource = new WebhookResource(Client(handler), "acc");

        (await resource.GetEndpointSecretAsync("ep1")).Secret.Should().Be("whsec_old");
        (await resource.RotateEndpointSecretAsync("ep1")).Secret.Should().Be("whsec_new");

        handler.Requests.Select(r => (r.Method, r.RequestUri!.AbsolutePath)).Should().Equal(
            (HttpMethod.Get, "/v1/accounts/acc/webhooks/endpoints/ep1/secret"),
            (HttpMethod.Post, "/v1/accounts/acc/webhooks/endpoints/ep1/secret/rotate"));
    }

    [Fact]
    public async Task ListDispatches_FiltersByEndpointAndReadsEndpointId()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/accounts/acc/webhooks",
            FakeHttpMessageHandler.ApiOk(new[] { new { id = "d1", @event = "document_ready", endpoint_id = "ep1" } }));

        var result = await new WebhookResource(Client(handler), "acc")
            .ListDispatchesAsync(new ListDispatchesParams { EndpointId = "ep1" });

        handler.Requests.Single().RequestUri!.Query.Should().Contain("endpoint_id=ep1");
        result.Data.Single().EndpointId.Should().Be("ep1");
    }

    // ---- Signature verification ------------------------------------------------------

    private const string Secret = "whsec_MfKQ9r8GKYqrTwjUPD8ILPZIo2LaLaSw";
    private const string Body = "{\"event\":\"document_ready\",\"id\":1}";
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_760_000_000);

    private static string Sign(string id, long timestamp, string body)
    {
        var key = Convert.FromBase64String(Secret["whsec_".Length..]);
        var mac = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{id}.{timestamp}.{body}"));
        return "v1," + Convert.ToBase64String(mac);
    }

    private static bool Verify(string? id, string? timestamp, string? signature, string body = Body, string secret = Secret) =>
        WebhookSignature.Verify(secret, id, timestamp, signature, body, timeProvider: new FixedTime(Now));

    [Fact]
    public void Verify_AcceptsValidSignatureAmongSeveral()
    {
        var ts = Now.ToUnixTimeSeconds();
        Verify("msg_1", ts.ToString(), $"v1,AAAA {Sign("msg_1", ts, Body)}").Should().BeTrue();
    }

    [Fact]
    public void Verify_AcceptsSecretWithoutPrefix()
    {
        var ts = Now.ToUnixTimeSeconds();
        Verify("msg_1", ts.ToString(), Sign("msg_1", ts, Body), secret: Secret["whsec_".Length..]).Should().BeTrue();
    }

    [Fact]
    public void Verify_RejectsTamperedBodyIdOrMissingHeaders()
    {
        var ts = Now.ToUnixTimeSeconds();
        var signature = Sign("msg_1", ts, Body);

        Verify("msg_1", ts.ToString(), signature, Body + " ").Should().BeFalse();
        Verify("msg_2", ts.ToString(), signature).Should().BeFalse();
        Verify(null, ts.ToString(), signature).Should().BeFalse();
        Verify("msg_1", "not-a-number", signature).Should().BeFalse();
        Verify("msg_1", ts.ToString(), null).Should().BeFalse();
        Verify("msg_1", ts.ToString(), "v2," + signature[3..]).Should().BeFalse();
    }

    [Fact]
    public void Verify_RejectsTimestampsOutsideTolerance()
    {
        var stale = Now.AddMinutes(-6).ToUnixTimeSeconds();
        var future = Now.AddMinutes(6).ToUnixTimeSeconds();
        var edge = Now.AddMinutes(-5).ToUnixTimeSeconds();

        Verify("msg_1", stale.ToString(), Sign("msg_1", stale, Body)).Should().BeFalse();
        Verify("msg_1", future.ToString(), Sign("msg_1", future, Body)).Should().BeFalse();
        Verify("msg_1", edge.ToString(), Sign("msg_1", edge, Body)).Should().BeTrue();
    }

    [Fact]
    public void Verify_RejectsMalformedSecret()
    {
        var act = () => Verify("msg_1", "1", "v1,x", secret: "whsec_not base64!");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WebhookEvent_DeserializesDeliveredBody()
    {
        const string json = """
            {"id":42,"event":"document_ready","message":null,"payload":null,"origin":{"ip":"203.0.113.1","user-agent":"x"},
             "created_at":1760000000,"subject":{"type":"Account","id":"acc"},"object":{"type":"Document","id":"doc"},"account_id":"acc"}
            """;

        var evt = JsonSerializer.Deserialize<WebhookEvent>(json)!;

        evt.Id.Should().Be(42);
        evt.Event.Should().Be("document_ready");
        evt.CreatedAt.Should().Be(1_760_000_000);
        evt.Object.GetProperty("type").GetString().Should().Be("Document");
        evt.Origin!.Value.GetProperty("ip").GetString().Should().Be("203.0.113.1");
        evt.AccountId.Should().Be("acc");
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // ---- Two-factor authentication ---------------------------------------------------

    [Fact]
    public async Task VerifyMfa_PostsChallengeWithoutCredential()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/authentication/mfa/verify",
            FakeHttpMessageHandler.ApiOk(new { access_token = "jwt", accounts = Array.Empty<object>() }));
        var resource = new AuthenticationResource(
            Client(handler), request => request.Headers.TryAddWithoutValidation("X-Api-Key", "secret"));

        var result = await resource.VerifyMfaAsync(new VerifyMfaRequest { MfaToken = "challenge", Code = "123456" });

        result.AccessToken.Should().Be("jwt");
        Sent(handler).Should().Be((HttpMethod.Post, "/v1/authentication/mfa/verify"));
        handler.Requests.Single().Headers.Contains("X-Api-Key").Should().BeFalse();
        LastBody(handler).GetProperty("mfa_token").GetString().Should().Be("challenge");
        LastBody(handler).GetProperty("code").GetString().Should().Be("123456");
    }

    [Fact]
    public async Task Login_ExposesPendingMfaToken()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/login", FakeHttpMessageHandler.ApiOk(new { mfa_token = "challenge" }));

        var result = await new AuthenticationResource(Client(handler))
            .LoginAsync(new LoginRequest { Email = "user@example.com", Password = "pw" });

        result.MfaToken.Should().Be("challenge");
        result.AccessToken.Should().BeEmpty();
    }

    [Fact]
    public async Task MfaEnrollment_RoundTripsDocumentedRoutesAndBodies()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/users/self/mfa", FakeHttpMessageHandler.ApiOk(new
        {
            methods = new[] { new { id = "m1", type = "Totp", label = "My phone", confirmed_at = "2026-09-09T14:21:03Z" } },
            recovery_codes_remaining = 8,
        }));
        handler.AddJsonResponse(HttpMethod.Post, "/users/self/mfa/totp", FakeHttpMessageHandler.ApiOk(new
        {
            id = "m2",
            secret = "GEZDGNBV",
            provisioning_uri = "otpauth://totp/x?secret=GEZDGNBV",
        }));
        handler.AddJsonResponse(HttpMethod.Put, "/users/self/mfa/totp/confirm",
            FakeHttpMessageHandler.ApiOk(new { recovery_codes = new[] { "ABCD-EFGH-JKMN" } }));
        handler.AddJsonResponse(HttpMethod.Post, "/users/self/mfa/recovery-codes",
            FakeHttpMessageHandler.ApiOk(new { recovery_codes = new[] { "WXYZ-WXYZ-WXYZ" } }));
        handler.AddJsonResponse(HttpMethod.Delete, "/users/self/mfa/m1",
            FakeHttpMessageHandler.ApiOk(new { is_mfa_enabled = false }));
        var users = new UserResource(Client(handler));

        var list = await users.ListMfaMethodsAsync();
        var enrollment = await users.StartTotpEnrollmentAsync(new StartTotpEnrollmentRequest { Label = "Laptop" });
        var confirmed = await users.ConfirmTotpEnrollmentAsync(new ConfirmTotpEnrollmentRequest
        {
            Id = enrollment.Id,
            Code = "123456",
            ReauthCode = "654321",
        });
        var regenerated = await users.RegenerateRecoveryCodesAsync(new MfaReauthenticationRequest { Password = "pw" });
        var removal = await users.DeleteMfaMethodAsync("m1", new MfaReauthenticationRequest { Code = "123456" });

        list.RecoveryCodesRemaining.Should().Be(8);
        list.Methods.Single().Type.Should().Be("Totp");
        enrollment.ProvisioningUri.Should().StartWith("otpauth://");
        confirmed.RecoveryCodes.Should().Equal("ABCD-EFGH-JKMN");
        regenerated.RecoveryCodes.Should().Equal("WXYZ-WXYZ-WXYZ");
        removal.IsMfaEnabled.Should().BeFalse();

        handler.Requests.Select(r => (r.Method, r.RequestUri!.AbsolutePath)).Should().Equal(
            (HttpMethod.Get, "/v1/users/self/mfa"),
            (HttpMethod.Post, "/v1/users/self/mfa/totp"),
            (HttpMethod.Put, "/v1/users/self/mfa/totp/confirm"),
            (HttpMethod.Post, "/v1/users/self/mfa/recovery-codes"),
            (HttpMethod.Delete, "/v1/users/self/mfa/m1"));

        var bodies = handler.RequestBodies.Where(b => b.Length > 0).Select(b => JsonDocument.Parse(b).RootElement).ToList();
        bodies[0].GetProperty("label").GetString().Should().Be("Laptop");
        bodies[1].GetProperty("id").GetString().Should().Be("m2");
        bodies[1].GetProperty("reauth_code").GetString().Should().Be("654321");
        bodies[1].TryGetProperty("password", out _).Should().BeFalse();
        bodies[2].GetProperty("password").GetString().Should().Be("pw");
        bodies[3].GetProperty("code").GetString().Should().Be("123456");
    }

    [Fact]
    public async Task MfaReauthentication_RequiresPasswordOrCode()
    {
        var users = new UserResource(Client(new FakeHttpMessageHandler()));
        await ((Func<Task>)(() => users.DeleteMfaMethodAsync("m1", new MfaReauthenticationRequest())))
            .Should().ThrowAsync<ValidationException>();
        await ((Func<Task>)(() => users.RegenerateRecoveryCodesAsync(new MfaReauthenticationRequest())))
            .Should().ThrowAsync<ValidationException>();
    }

    // ---- Signers ---------------------------------------------------------------------

    [Fact]
    public async Task CreateSigner_SendsGovernmentId()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/signers",
            FakeHttpMessageHandler.ApiOk(new { id = "s1", full_name = "Ana", government_id = "12345678901" }));

        var signer = await new SignerResource(Client(handler), "acc").CreateAsync(new CreateSignerRequest
        {
            FullName = "Ana",
            Email = "ana@example.com",
            GovernmentId = "123.456.789-01",
        });

        LastBody(handler).GetProperty("government_id").GetString().Should().Be("123.456.789-01");
        signer.GovernmentId.Should().Be("12345678901");
    }
}
