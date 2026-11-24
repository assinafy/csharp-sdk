using System.Net;
using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class ScheduledSendResourceTests
{
    private static AssinafyClient Client(FakeHttpMessageHandler handler) => new(
        new AssinafyClientOptions { ApiKey = "test-key", AccountId = "acc" },
        FakeHttpMessageHandler.CreateClient(handler));

    [Fact]
    public async Task ScheduleAssignment_PreservesCreationPayloadAndOffset()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/documents/doc%2Fid/scheduled-sends", FakeHttpMessageHandler.ApiOk(new
        {
            id = "scheduled",
            type = "assignment",
            status = "pending",
            document_ids = new[] { "doc/id" },
            send_at = "2026-12-01T12:00:00Z",
            assignment_id = (string?)null,
            failure_reason = (string?)null,
        }));
        using var client = Client(handler);
        var result = await client.ScheduledSends.ScheduleAssignmentAsync("doc/id", new CreateAssignmentRequest
        {
            Method = "virtual",
            MaxTotalCredits = 0,
            Message = "Please sign",
            CopyReceivers = ["copy"],
            ExpiresAt = "2026-12-03T12:00:00Z",
            Signers = [new SignerRef { Id = "signer", SignAs = "Witness", Embedded = true, Step = 1,
                VerificationMethod = "Email", NotificationMethods = ["Email"] }],
        }, new DateTimeOffset(2026, 12, 1, 9, 0, 0, TimeSpan.FromHours(-3)));

        result.Id.Should().Be("scheduled");
        result.DocumentIds.Should().Equal("doc/id");
        result.AssignmentId.Should().BeNull();
        result.FailureReason.Should().BeNull();
        var request = handler.Requests.Single();
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.PathAndQuery.Should().Be("/v1/documents/doc%2Fid/scheduled-sends");
        request.Headers.GetValues("X-Api-Key").Should().Equal("test-key");
        request.Headers.Contains("Idempotency-Key").Should().BeFalse();
        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        body.RootElement.GetProperty("send_at").GetInt64().Should().Be(
            new DateTimeOffset(2026, 12, 1, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds());
        body.RootElement.GetProperty("max_total_credits").GetDecimal().Should().Be(0);
        body.RootElement.GetProperty("message").GetString().Should().Be("Please sign");
        body.RootElement.GetProperty("expires_at").GetString().Should().Be("2026-12-03T12:00:00Z");
        body.RootElement.GetProperty("copy_receivers")[0].GetString().Should().Be("copy");
        var signer = body.RootElement.GetProperty("signers")[0];
        signer.GetProperty("sign_as").GetString().Should().Be("Witness");
        signer.GetProperty("embedded").GetBoolean().Should().BeTrue();
        signer.GetProperty("step").GetInt32().Should().Be(1);
        signer.GetProperty("notification_methods")[0].GetString().Should().Be("Email");
    }

    [Fact]
    public async Task ScheduleEnvelope_FlattensCreationBodyAndUsesExplicitWorkspace()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/other%2Facc/scheduled-sends",
            FakeHttpMessageHandler.ApiOk(new { id = "scheduled", type = "envelope", status = "pending" }));
        using var client = Client(handler);
        var result = await client.ScheduledSends.ScheduleEnvelopeAsync(new CreateEnvelopeRequest
        {
            Name = "Contract",
            Message = "Review",
            MaxTotalCredits = 0,
            Signers = [new EnvelopeSignerRequest { Id = "signer", Embedded = true }],
            Documents = [new EnvelopeDocumentRequest { Id = "doc", SignerIds = ["signer"], CopyReceiverIds = ["copy"] }],
        }, DateTimeOffset.Parse("2026-12-01T12:00:00Z"), "other/acc");

        result.Type.Should().Be("envelope");
        handler.Requests.Single().RequestUri!.PathAndQuery.Should().Be("/v1/accounts/other%2Facc/scheduled-sends");
        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        body.RootElement.GetProperty("name").GetString().Should().Be("Contract");
        body.RootElement.GetProperty("message").GetString().Should().Be("Review");
        body.RootElement.GetProperty("max_total_credits").GetDecimal().Should().Be(0);
        body.RootElement.GetProperty("documents")[0].GetProperty("copy_receiver_ids")[0].GetString().Should().Be("copy");
        body.RootElement.GetProperty("signers")[0].GetProperty("embedded").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("send_at").GetInt64().Should().Be(
            new DateTimeOffset(2026, 12, 1, 12, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds());
    }

    [Fact]
    public async Task ListCancelAndSendNow_UseDocumentedMethodsAndPreserveResults()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/accounts/acc/scheduled-sends", FakeHttpMessageHandler.ApiOk(new[]
        {
            new { id = "scheduled", status = "failed", failure_reason = "No documents left" },
        }), headers: new Dictionary<string, string> { ["x-pagination-total-count"] = "21" });
        handler.AddJsonResponse(HttpMethod.Put, "/scheduled%2Fid/cancel",
            FakeHttpMessageHandler.ApiOk(new { id = "scheduled/id", status = "cancelled", processed_at = "2026-12-01T12:00:00Z" }));
        handler.AddJsonResponse(HttpMethod.Put, "/scheduled%2Fid/send",
            FakeHttpMessageHandler.ApiOk(new { id = "scheduled/id", status = "sent", assignment_id = "assignment" }));
        using var client = Client(handler);

        var page = await client.ScheduledSends.ListAsync(new Dictionary<string, string?> { ["document_id"] = "doc/id", ["page"] = "2" });
        page.Meta!.Total.Should().Be(21);
        page.Data.Single().FailureReason.Should().Be("No documents left");
        (await client.ScheduledSends.CancelAsync("scheduled/id")).Status.Should().Be("cancelled");
        (await client.ScheduledSends.SendNowAsync("scheduled/id")).AssignmentId.Should().Be("assignment");
        handler.Requests.Select(r => r.RequestUri!.PathAndQuery).Should().Equal(
            "/v1/accounts/acc/scheduled-sends?document_id=doc%2Fid&page=2",
            "/v1/accounts/acc/scheduled-sends/scheduled%2Fid/cancel",
            "/v1/accounts/acc/scheduled-sends/scheduled%2Fid/send");
        handler.RequestBodies.Should().OnlyContain(body => body == "");
    }

    [Fact]
    public async Task SendNow_DoesNotRetryARefusedCreditCeiling()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/scheduled/send", new
        {
            status = 409,
            message = "Ceiling exceeded",
            code = "CreditCeilingExceeded",
            total_credits = 1.5m,
            max_total_credits = 0,
        }, HttpStatusCode.Conflict);
        using var client = Client(handler);
        var act = () => client.ScheduledSends.SendNowAsync("scheduled");
        var error = (await act.Should().ThrowAsync<ApiException>()).Which;
        error.Details!.Value.GetProperty("code").GetString().Should().Be("CreditCeilingExceeded");
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public void ScheduledSends_RejectInvalidPathsAndRequestsBeforeSending()
    {
        var handler = new FakeHttpMessageHandler();
        using var client = Client(handler);
        Action cancel = () => client.ScheduledSends.CancelAsync("..");
        cancel.Should().Throw<ValidationException>();
        Action assignment = () => client.ScheduledSends.ScheduleAssignmentAsync("doc", new CreateAssignmentRequest(), DateTimeOffset.UtcNow);
        assignment.Should().Throw<ValidationException>();
        Action envelope = () => client.ScheduledSends.ScheduleEnvelopeAsync(new CreateEnvelopeRequest
        {
            Name = "Envelope",
            Signers = [new EnvelopeSignerRequest { Id = "signer" }],
            Documents = [new EnvelopeDocumentRequest { Id = "doc", SignerIds = ["signer"] }],
            MaxTotalCredits = -1,
        }, DateTimeOffset.UtcNow);
        envelope.Should().Throw<ValidationException>();
        handler.Requests.Should().BeEmpty();
    }
}
