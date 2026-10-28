using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class CurrentPayloadTests
{
    [Theory]
    [InlineData("123", 123L)]
    [InlineData("\"1970-01-01T00:00:00Z\"", 0L)]
    [InlineData("\"1970-01-01T01:30:00+01:30\"", 0L)]
    [InlineData("\"1969-12-31T23:59:59.999Z\"", -1L)]
    public async Task WhatsAppHistory_AcceptsIsoAndLegacyTimestamps(string timestamp, long expected)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/whatsapp-notifications",
            $$$"""{"status":200,"message":"OK","data":[{"sent_at":{{{timestamp}}},"signer_id":"signer"}]}""");
        using var http = FakeHttpMessageHandler.CreateClient(handler);

        var result = await new AssignmentResource(http).ListWhatsAppNotificationsAsync("document", "assignment");

        result.Should().ContainSingle().Which.SentAt.Should().Be(expected);
        using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(result[0]));
        serialized.RootElement.GetProperty("sent_at").GetInt64().Should().Be(expected);
        handler.Requests.Single().RequestUri!.AbsolutePath.Should()
            .Be("/v1/documents/document/assignments/assignment/whatsapp-notifications");
    }

    [Theory]
    [InlineData("\"not a timestamp\"")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("true")]
    [InlineData("1.5")]
    [InlineData("9223372036854775808")]
    public async Task WhatsAppHistory_RejectsInvalidTimestampInsideSdkException(string timestamp)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/whatsapp-notifications",
            $$$"""{"status":200,"message":"OK","data":[{"sent_at":{{{timestamp}}}}]}""");
        using var http = FakeHttpMessageHandler.CreateClient(handler);

        await ((Func<Task>)(() => new AssignmentResource(http)
                .ListWhatsAppNotificationsAsync("document", "assignment")))
            .Should().ThrowAsync<SerializationException>();
    }

    [Theory]
    [InlineData("null", null)]
    [InlineData("0", 0)]
    [InlineData("10", 10)]
    public async Task ResendCost_DistinguishesUnlimitedFromExhaustedBalance(string balance, int? expected)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Post, "/estimate-resend-cost",
            $$$"""{"status":200,"message":"OK","data":{"total":0.45,"breakdown":[{"code":"NotificationWhatsappResend","name":"WhatsApp reminder","cost":0.45}],"credit_balance":{{{balance}}},"has_sufficient_credits":true}}""");
        using var http = FakeHttpMessageHandler.CreateClient(handler);

        var result = await new AssignmentResource(http)
            .EstimateResendCostAsync("document", "assignment", "signer");

        result.AvailableCreditBalance.Should().Be(expected);
        result.CreditBalance.Should().Be(expected ?? 0);
        result.Total.Should().Be(0.45m);
        result.Breakdown.Should().ContainSingle().Which.Cost.Should().Be(0.45m);
        result.HasSufficientCredits.Should().BeTrue();
        handler.Requests.Single().RequestUri!.AbsolutePath.Should()
            .Be("/v1/documents/document/assignments/assignment/signers/signer/estimate-resend-cost");
        handler.RequestBodies.Single().Should().BeEmpty();
    }

    [Fact]
    public void ResendCost_LegacyInitializerAndSerializationPreserveBalance()
    {
        var original = new ResendCostEstimate { CreditBalance = 12.5m, TotalCredits = 0.45m };
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(original));

        json.RootElement.GetProperty("credit_balance").GetDecimal().Should().Be(12.5m);
        json.RootElement.TryGetProperty("CreditBalance", out _).Should().BeFalse();
        var restored = json.RootElement.Deserialize<ResendCostEstimate>()!;
        restored.CreditBalance.Should().Be(12.5m);
        restored.AvailableCreditBalance.Should().Be(12.5m);
        restored.TotalCredits.Should().Be(0.45m);

        var missing = JsonSerializer.Deserialize<ResendCostEstimate>("{\"total_credits\":0.45}")!;
        missing.AvailableCreditBalance.Should().BeNull();
        missing.CreditBalance.Should().Be(0);
    }

    [Fact]
    public async Task DocumentDetails_ExposeVerificationAndContractPresenceWithoutAssumingMissingIsFalse()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/document", FakeHttpMessageHandler.ApiOk(new
        {
            id = "document",
            verification_code = "verification-code",
            verification_url = "https://www.assinafy.com.br/verificador/verification-code",
            has_contract = true,
            assignment = new { id = "assignment", document_id = "document", created_at = "2026-10-23T12:00:00Z" },
        }));
        using var http = FakeHttpMessageHandler.CreateClient(handler);

        var document = await new DocumentResource(http).GetAsync("document");

        document.VerificationCode.Should().Be("verification-code");
        document.VerificationUrl.Should().EndWith("/verification-code");
        document.HasContract.Should().BeTrue();
        document.Assignment!.DocumentId.Should().Be("document");
        document.Assignment.CreatedAt.Should().Be("2026-10-23T12:00:00Z");
        JsonSerializer.Deserialize<DocumentDetails>("{\"id\":\"legacy\"}")!.HasContract.Should().BeNull();
        JsonSerializer.Deserialize<DocumentDetails>("{\"has_contract\":false}")!.HasContract.Should().BeFalse();
    }

    [Fact]
    public async Task AccountDetails_ExposePlanEndpointLimit()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/accounts/account", FakeHttpMessageHandler.ApiOk(new
        {
            id = "account",
            limits = new { webhook_endpoints = 3 },
        }));
        using var http = FakeHttpMessageHandler.CreateClient(handler);

        var account = await new AccountResource(http, "account").GetAsync();

        account.Limits!.WebhookEndpoints.Should().Be(3);
        JsonSerializer.Deserialize<Account>("{\"id\":\"legacy\"}")!.Limits.Should().BeNull();
    }

    [Fact]
    public async Task WebhookDelivery_ExposesPauseAndRetryState()
    {
        var handler = new FakeHttpMessageHandler();
        var endpoint = new
        {
            resource = "activity_event_subscription",
            id = "endpoint",
            is_active = true,
            is_held = true,
            held_since = 1790000000L,
        };
        handler.AddJsonResponse(HttpMethod.Get, "/accounts/account/webhooks", FakeHttpMessageHandler.ApiOk(new[]
        {
            new { id = "dispatch", webhook_id = "stable-delivery-id", attempt = 2, kind = "probe", will_retry = true },
        }));
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        var resource = new WebhookResource(http, "account");

        handler.AddJsonResponse(HttpMethod.Get, "/webhooks/endpoints/endpoint", FakeHttpMessageHandler.ApiOk(endpoint));
        handler.AddJsonResponse(HttpMethod.Get, "/webhooks/subscriptions", FakeHttpMessageHandler.ApiOk(endpoint));
        var current = await resource.GetEndpointAsync("endpoint");
        var subscription = await resource.GetAsync();
        var dispatches = await resource.ListDispatchesAsync();

        current.Resource.Should().Be("activity_event_subscription");
        current.IsHeld.Should().BeTrue();
        current.HeldSince.Should().Be(1790000000L);
        subscription.IsHeld.Should().BeTrue();
        subscription.HeldSince.Should().Be(1790000000L);
        var dispatch = dispatches.Data.Should().ContainSingle().Which;
        dispatch.WebhookId.Should().Be("stable-delivery-id");
        dispatch.Attempt.Should().Be(2);
        dispatch.Kind.Should().Be("probe");
        dispatch.WillRetry.Should().BeTrue();
        JsonSerializer.Deserialize<WebhookEndpoint>("{}")!.HeldSince.Should().BeNull();
        JsonSerializer.Deserialize<WebhookDispatch>("{}")!.Attempt.Should().BeNull();
    }

    [Fact]
    public async Task ContractNotificationPreferences_ReadAllNewFlags()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/users/self/notification-preferences", FakeHttpMessageHandler.ApiOk(new
        {
            ContractRenewalNotice = true,
            ContractExpiring = false,
            ContractExpired = true,
            ContractRenewed = false,
        }));
        using var http = FakeHttpMessageHandler.CreateClient(handler);

        var preferences = await new UserResource(http).GetNotificationPreferencesAsync();

        preferences.ContractRenewalNotice.Should().BeTrue();
        preferences.ContractExpiring.Should().BeFalse();
        preferences.ContractExpired.Should().BeTrue();
        preferences.ContractRenewed.Should().BeFalse();
    }
}
