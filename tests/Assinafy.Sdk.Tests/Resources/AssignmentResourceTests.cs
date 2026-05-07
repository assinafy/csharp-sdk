using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class AssignmentResourceTests
{
    private static AssignmentResource CreateResource(FakeHttpMessageHandler handler)
        => new(FakeHttpMessageHandler.CreateClient(handler));

    [Fact]
    public void BuildPayload_NormalisesStringSignerIds()
    {
        var body = AssignmentResource.BuildPayload(new CreateAssignmentRequest
        {
            Signers = ["a", "b"],
        });

        body["method"].Should().Be("virtual");
        var signers = (List<Dictionary<string, object?>>)body["signers"]!;
        signers.Should().HaveCount(2);
        signers[0]["id"].Should().Be("a");
        signers[1]["id"].Should().Be("b");
    }

    [Fact]
    public void BuildPayload_AcceptsLegacySignerIds()
    {
        var body = AssignmentResource.BuildPayload(new CreateAssignmentRequest
        {
            SignerIds = ["a"],
        });

        var signers = (List<Dictionary<string, object?>>)body["signers"]!;
        signers[0]["id"].Should().Be("a");
    }

    [Fact]
    public void BuildPayload_AllowsEstimationWithoutSignerIds()
    {
        var body = AssignmentResource.BuildPayload(
            new CreateAssignmentRequest
            {
                Signers = [new SignerRef { VerificationMethod = "Whatsapp" }, new SignerRef()],
            },
            allowSignersWithoutId: true);

        var signers = (List<Dictionary<string, object?>>)body["signers"]!;
        signers[0]["verification_method"].Should().Be("Whatsapp");
        signers.Should().HaveCount(2);
    }

    [Fact]
    public void BuildPayload_IncludesOptionalFieldsWhenProvided()
    {
        var body = AssignmentResource.BuildPayload(new CreateAssignmentRequest
        {
            Signers = ["a"],
            Message = "hi",
            ExpiresAt = "2026-12-31T00:00:00Z",
            CopyReceivers = ["c"],
        });

        body["message"].Should().Be("hi");
        body["expires_at"].Should().Be("2026-12-31T00:00:00Z");
        body["copy_receivers"].Should().BeEquivalentTo(new[] { "c" });
    }

    [Fact]
    public void BuildPayload_ThrowsOnEmptySigners()
    {
        var act = () => AssignmentResource.BuildPayload(new CreateAssignmentRequest { Signers = [] });
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public async Task Create_PostsToCorrectUrlWithNormalisedBody()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/documents/doc-1/assignments",
            FakeHttpMessageHandler.ApiOk(new { id = "assignment-1", method = "virtual" }));

        var resource = CreateResource(handler);
        var result = await resource.CreateAsync("doc-1", new CreateAssignmentRequest
        {
            Signers = ["s1", "s2"],
        });

        result.Id.Should().Be("assignment-1");
        var postBody = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0));
        postBody.RootElement.GetProperty("method").GetString().Should().Be("virtual");
        postBody.RootElement.GetProperty("signers").EnumerateArray().Should().HaveCount(2);
    }

    [Fact]
    public async Task EstimateCost_AcceptsSignerDescriptorsWithoutIds()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/estimate-cost",
            FakeHttpMessageHandler.ApiOk(new { documents = 1, total_credits = 0.45m, has_sufficient_resources = true }));

        var resource = CreateResource(handler);
        var result = await resource.EstimateCostAsync("doc-1", new CreateAssignmentRequest
        {
            Signers = [new SignerRef { VerificationMethod = "Whatsapp" }],
        });

        result.TotalCredits.Should().Be(0.45m);
        var postBody = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0));
        var signers = postBody.RootElement.GetProperty("signers").EnumerateArray().ToList();
        signers[0].GetProperty("verification_method").GetString().Should().Be("Whatsapp");
        signers[0].TryGetProperty("id", out _).Should().BeFalse();
    }

    [Fact]
    public async Task ResetExpiration_SerializesNullExpiration()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/reset-expiration",
            FakeHttpMessageHandler.ApiOk(new { id = "assignment-1", expires_at = (string?)null }));

        var resource = CreateResource(handler);
        await resource.ResetExpirationAsync("doc-1", "assignment-1", null);

        var body = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0));
        body.RootElement.GetProperty("expires_at").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task ListWhatsAppNotifications_CallsDocumentedEndpoint()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/whatsapp-notifications",
            FakeHttpMessageHandler.ApiOk(new[]
            {
                new { sent_at = 1710000000, header = "H", body = "B", buttons = Array.Empty<object>(), phone_number = "+5511999990001", signer_id = "s1" },
            }));

        var resource = CreateResource(handler);
        var result = await resource.ListWhatsAppNotificationsAsync("doc-1", "assignment-1");

        result.Should().ContainSingle(n => n.SignerId == "s1");
    }
}
