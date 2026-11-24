using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class DocumentNotificationTests
{
    [Fact]
    public async Task Watchers_ReadReplaceAndClearTheWholeList()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/watchers", FakeHttpMessageHandler.ApiOk(new[] { new { id = "contact", email = "watcher@example.com" } }));
        handler.AddJsonResponse(HttpMethod.Put, "/watchers", FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));
        using var client = new AssinafyClient(new AssinafyClientOptions { ApiKey = "key" }, FakeHttpMessageHandler.CreateClient(handler));

        (await client.Documents.ListWatchersAsync("doc/id")).Single().Id.Should().Be("contact");
        await client.Documents.SetWatchersAsync("doc/id", ["contact"]);
        (await client.Documents.SetWatchersAsync("doc/id", [])).Should().BeEmpty();

        handler.Requests.Select(r => r.Method).Should().Equal(HttpMethod.Get, HttpMethod.Put, HttpMethod.Put);
        handler.Requests.Should().OnlyContain(r => r.RequestUri!.PathAndQuery == "/v1/documents/doc%2Fid/watchers" && r.Headers.Contains("X-Api-Key"));
        handler.RequestBodies.Should().Equal("", "{\"watcher_ids\":[\"contact\"]}", "{\"watcher_ids\":[]}");
        Action tooMany = () => client.Documents.SetWatchersAsync("doc", Enumerable.Range(0, 11).Select(i => i.ToString()).ToArray());
        tooMany.Should().Throw<ValidationException>();
        Action blank = () => client.Documents.SetWatchersAsync("doc", [" "]);
        blank.Should().Throw<ValidationException>();
        Action traversal = () => client.Documents.ListWatchersAsync("..");
        traversal.Should().Throw<ValidationException>();
        handler.Requests.Should().HaveCount(3);
    }

    [Fact]
    public async Task CapacitiesAndHistory_PreserveSignerOrderAndNullableDeliveryTimes()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/signer-capacities", FakeHttpMessageHandler.ApiOk(new[]
        {
            new { signer_id = "s1", sign_as = "Witness" }, new { signer_id = "s2", sign_as = "Sign" },
        }));
        handler.AddJsonResponse(HttpMethod.Get, "/notification-history", FakeHttpMessageHandler.ApiOk(new[]
        {
            new { signer_id = "s1", notification_history = new[] { new { @event = "Email", status = "delivered",
                sent_at = "2026-11-20T12:00:00Z", delivered_at = "2026-11-20T12:00:05Z", opened_at = (string?)null } } },
        }));
        using var client = new AssinafyClient(new AssinafyClientOptions { Token = "token" }, FakeHttpMessageHandler.CreateClient(handler));
        (await client.Assignments.GetSignerCapacitiesAsync("doc/id")).Select(s => s.SignAs).Should().Equal("Witness", "Sign");
        var history = (await client.Assignments.GetNotificationHistoryAsync("doc/id")).Single();
        history.SignerId.Should().Be("s1");
        history.NotificationHistory.Single().DeliveredAt.Should().Be("2026-11-20T12:00:05Z");
        history.NotificationHistory.Single().OpenedAt.Should().BeNull();
        handler.Requests.Select(r => r.RequestUri!.PathAndQuery).Should().Equal(
            "/v1/documents/doc%2Fid/signer-capacities", "/v1/documents/doc%2Fid/notification-history");
        handler.Requests.Should().OnlyContain(r => r.Method == HttpMethod.Get && r.Headers.Authorization!.Parameter == "token");
    }

    [Fact]
    public async Task SignAs_IsSentForAssignmentCreationAndEstimation()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/assignments", FakeHttpMessageHandler.ApiOk(new { id = "assignment" }));
        using var client = new AssinafyClient(new AssinafyClientOptions(), FakeHttpMessageHandler.CreateClient(handler));
        var request = new CreateAssignmentRequest { Signers = [new SignerRef { Id = "s1", SignAs = "Witness" }] };
        await client.Assignments.CreateAsync("doc", request);
        await client.Assignments.EstimateCostAsync("doc", request);
        foreach (var json in handler.RequestBodies)
        {
            using var body = JsonDocument.Parse(json);
            body.RootElement.GetProperty("signers")[0].GetProperty("sign_as").GetString().Should().Be("Witness");
        }
        using var estimate = JsonDocument.Parse(handler.RequestBodies[1]);
        estimate.RootElement.GetProperty("signers")[0].TryGetProperty("id", out _).Should().BeFalse();
    }

    [Fact]
    public async Task EnvelopeReminderEstimate_UsesPostWithoutSendingAReminder()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/estimate-resend-cost", FakeHttpMessageHandler.ApiOk(new
        {
            total_credits = 0,
            credit_balance = (decimal?)null,
            has_sufficient_credits = true,
        }));
        using var client = new AssinafyClient(new AssinafyClientOptions { ApiKey = "key", AccountId = "default" }, FakeHttpMessageHandler.CreateClient(handler));
        var result = await client.Envelopes.EstimateResendCostAsync("env/id", "signer/id", "acc/id");
        result.TotalCredits.Should().Be(0);
        result.AvailableCreditBalance.Should().BeNull();
        handler.Requests.Single().RequestUri!.PathAndQuery.Should().Be("/v1/accounts/acc%2Fid/envelopes/env%2Fid/signers/signer%2Fid/estimate-resend-cost");
        handler.RequestBodies.Single().Should().BeEmpty();
    }
}
