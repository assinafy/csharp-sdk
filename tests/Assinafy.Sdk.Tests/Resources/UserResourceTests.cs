using System.Text.Json;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class UserResourceTests
{
    private static UserResource CreateResource(FakeHttpMessageHandler handler)
        => new(FakeHttpMessageHandler.CreateClient(handler));

    private static object User(string id) => new
    {
        id,
        name = "API User",
        email = "user@example.test",
        telephone = (string?)null,
        government_id = "15774136604",
        is_email_verified = true,
        has_accepted_terms = true,
        created_at = "2026-06-01T12:00:00Z",
        to_be_deleted_at = (string?)null,
    };

    private static object Preferences(bool signerDeclined = true) => new
    {
        DocumentCompleted = true,
        SignerDeclined = signerDeclined,
        DocumentCancelled = true,
        DocumentAboutToExpire = true,
        DocumentExpired = true,
        DocumentExpirationReset = true,
        DocumentProcessingFailed = true,
        TemplateProcessingFailed = true,
        SignerWhatsappFailed = true,
    };

    private static object StatsRow() => new
    {
        period = "2026-06-01",
        documents_uploaded = 42,
        documents_sent = 37,
        signature_requests = 61,
        signature_requests_email = 55,
        signature_requests_whatsapp = 18,
        signature_requests_viewed = 44,
        signature_requests_completed = 52,
        documents_certified = 30,
    };

    [Fact]
    public async Task GetSelf_DeserializesProductionDirectUserData()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/users/self",
            FakeHttpMessageHandler.ApiOk(User("user-1")));

        var result = await CreateResource(handler).GetSelfAsync();

        result.Id.Should().Be("user-1");
        result.Email.Should().Be("user@example.test");
        result.GovernmentId.Should().Be("15774136604");
    }

    [Fact]
    public async Task GetSelf_DeserializesSandboxLegacyUserEnvelopeData()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/users/self",
            FakeHttpMessageHandler.ApiOk(new
            {
                user = User("legacy-user"),
                accounts = new[] { new { id = "acc", name = "Workspace" } },
            }));

        var result = await CreateResource(handler).GetSelfAsync();

        result.Id.Should().Be("legacy-user");
        result.Name.Should().Be("API User");
    }

    [Fact]
    public async Task GetNotificationPreferences_DeserializesPascalCaseKeys()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/users/self/notification-preferences",
            FakeHttpMessageHandler.ApiOk(Preferences(signerDeclined: false)));

        var result = await CreateResource(handler).GetNotificationPreferencesAsync();

        result.DocumentCompleted.Should().BeTrue();
        result.SignerDeclined.Should().BeFalse();
        result.SignerWhatsappFailed.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateNotificationPreferences_SendsOnlySetPascalCaseKeys()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/users/self/notification-preferences",
            FakeHttpMessageHandler.ApiOk(Preferences(signerDeclined: false)));

        var result = await CreateResource(handler).UpdateNotificationPreferencesAsync(
            new UpdateNotificationPreferencesRequest
            {
                SignerDeclined = false,
                SignerWhatsappFailed = true,
            });

        result.SignerDeclined.Should().BeFalse();
        using var body = JsonDocument.Parse(handler.RequestBodies.Single(b => b.Length > 0));
        body.RootElement.EnumerateObject().Select(property => property.Name)
            .Should().BeEquivalentTo("SignerDeclined", "SignerWhatsappFailed");
        body.RootElement.GetProperty("SignerDeclined").GetBoolean().Should().BeFalse();
        body.RootElement.GetProperty("SignerWhatsappFailed").GetBoolean().Should().BeTrue();
        body.RootElement.TryGetProperty("signer_declined", out _).Should().BeFalse();
    }

    [Fact]
    public async Task GetStats_SendsGranularityAndMonthAndDeserializesEveryKpi()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/users/self/stats?granularity=daily&month=2026-06",
            FakeHttpMessageHandler.ApiOk(new[] { StatsRow() }));

        var result = await CreateResource(handler).GetStatsAsync(new DocumentStatsParams
        {
            Granularity = DocumentStatsGranularities.Daily,
            Month = "2026-06",
        });

        var row = result.Should().ContainSingle().Which;
        row.Period.Should().Be("2026-06-01");
        row.DocumentsUploaded.Should().Be(42);
        row.DocumentsSent.Should().Be(37);
        row.SignatureRequests.Should().Be(61);
        row.SignatureRequestsEmail.Should().Be(55);
        row.SignatureRequestsWhatsapp.Should().Be(18);
        row.SignatureRequestsViewed.Should().Be(44);
        row.SignatureRequestsCompleted.Should().Be(52);
        row.DocumentsCertified.Should().Be(30);
    }

    [Fact]
    public void Client_ExposesUserResource()
    {
        using var client = new AssinafyClient(new AssinafyClientOptions { ApiKey = "test-key" });

        client.Users.Should().NotBeNull();
    }

    [Fact]
    public async Task StatsAndPreferenceRequests_RejectInvalidEmptyInputs()
    {
        var resource = CreateResource(new FakeHttpMessageHandler());

        await ((Func<Task>)(() => resource.GetStatsAsync(new DocumentStatsParams
        {
            Granularity = DocumentStatsGranularities.Daily,
        }))).Should().ThrowAsync<Assinafy.Sdk.Exceptions.ValidationException>();

        await ((Func<Task>)(() => resource.UpdateNotificationPreferencesAsync(new())))
            .Should().ThrowAsync<Assinafy.Sdk.Exceptions.ValidationException>();
    }
}
