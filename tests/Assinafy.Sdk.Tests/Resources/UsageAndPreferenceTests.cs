using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class UsageAndPreferenceTests
{
    private static AssinafyClient Client(FakeHttpMessageHandler handler) =>
        new(new AssinafyClientOptions { ApiKey = "example-key", AccountId = "acc" }, FakeHttpMessageHandler.CreateClient(handler));

    [Fact]
    public async Task UsageAndSigningFeatures_PreserveUnlimitedValuesAndExactFeatureKeys()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/usage", """
            {"status":200,"data":{"plan":null,"documents_renewal_date":null,"balances":{
              "documents":{"balance":null,"limit":null,"spent":3},
              "credits":{"balance":12.75,"limit":100,"spent":87.25}}}}
            """);
        handler.AddRawResponse(HttpMethod.Get, "expand=signing_features", """
            {"status":200,"data":{"id":"acc","signing_features":{"DigitalCertificate":true,
             "VerificationIdDocumentFaceMatchBasic":true,"WhatsappNotification":true,"VerificationBypass":false,"Embed":true}}}
            """);
        using var client = Client(handler);
        var usage = await client.Accounts.GetUsageAsync("override");
        usage.Plan.Should().BeNull();
        usage.DocumentsRenewalDate.Should().BeNull();
        usage.Balances.Documents.Balance.Should().BeNull();
        usage.Balances.Documents.Limit.Should().BeNull();
        usage.Balances.Documents.Spent.Should().Be(3);
        usage.Balances.Credits.Balance.Should().Be(12.75m);
        var account = await client.Accounts.GetWithSigningFeaturesAsync();
        account.SigningFeatures!["DigitalCertificate"].Should().BeTrue();
        account.SigningFeatures["VerificationBypass"].Should().BeFalse();
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/v1/accounts/override/usage");
        handler.Requests[1].RequestUri!.PathAndQuery.Should().Be("/v1/accounts/acc?expand=signing_features");
        handler.Requests.Should().OnlyContain(r => r.Method == HttpMethod.Get && r.Headers.Contains("X-Api-Key"));
    }

    [Fact]
    public async Task NotificationPolicy_MergesOnlySuppliedSettingsWithoutChangingEventCasing()
    {
        const string policy = """
            {"status":200,"data":{"editable":true,"owners_receive_all":false,
             "events":{"DocumentCompleted":true,"SignerViewed":false,"FutureEvent":true}}}
            """;
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/notification-policy", policy);
        handler.AddRawResponse(HttpMethod.Put, "/notification-policy", policy);
        using var client = Client(handler);
        (await client.Accounts.GetNotificationPolicyAsync()).Events["FutureEvent"].Should().BeTrue();
        await client.Accounts.UpdateNotificationPolicyAsync(new()
        {
            Events = new Dictionary<string, bool> { ["SignerViewed"] = false },
        }, "override");
        await client.Accounts.UpdateNotificationPolicyAsync(new() { OwnersReceiveAll = false });

        handler.Requests.Select(r => r.Method).Should().Equal(HttpMethod.Get, HttpMethod.Put, HttpMethod.Put);
        handler.Requests[1].RequestUri!.AbsolutePath.Should().Be("/v1/accounts/override/notification-policy");
        using var eventsBody = JsonDocument.Parse(handler.RequestBodies[1]);
        eventsBody.RootElement.EnumerateObject().Should().ContainSingle();
        eventsBody.RootElement.GetProperty("events").GetProperty("SignerViewed").GetBoolean().Should().BeFalse();
        using var ownersBody = JsonDocument.Parse(handler.RequestBodies[2]);
        ownersBody.RootElement.EnumerateObject().Should().ContainSingle();
        ownersBody.RootElement.GetProperty("owners_receive_all").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task PushPreferences_PreserveFutureKeysAndSendOnlySelectedChanges()
    {
        const string preferences = """
            {"status":200,"data":{"SignerViewed":false,"SignerIdentityReviewRequired":true,"FutureEvent":true}}
            """;
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/push-preferences", preferences);
        handler.AddRawResponse(HttpMethod.Put, "/push-preferences", preferences);
        using var client = Client(handler);
        (await client.Users.GetPushPreferencesAsync())["FutureEvent"].Should().BeTrue();
        var changed = await client.Users.UpdatePushPreferencesAsync(new Dictionary<string, bool> { ["SignerViewed"] = false });
        changed["SignerIdentityReviewRequired"].Should().BeTrue();
        handler.Requests.Select(r => r.Method).Should().Equal(HttpMethod.Get, HttpMethod.Put);
        handler.Requests.Should().OnlyContain(r => r.RequestUri!.AbsolutePath == "/v1/users/self/push-preferences");
        using var body = JsonDocument.Parse(handler.RequestBodies[1]);
        body.RootElement.EnumerateObject().Should().ContainSingle();
        body.RootElement.GetProperty("SignerViewed").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task EmptyPreferenceChanges_AreRejectedWithoutHttp()
    {
        var handler = new FakeHttpMessageHandler();
        using var client = Client(handler);
        await Assert.ThrowsAsync<ValidationException>(() => client.Accounts.UpdateNotificationPolicyAsync(new()));
        await Assert.ThrowsAsync<ValidationException>(() => client.Users.UpdatePushPreferencesAsync(new Dictionary<string, bool>()));
        handler.Requests.Should().BeEmpty();
    }
}
