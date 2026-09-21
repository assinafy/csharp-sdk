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
    private static AssignmentResource CreateResource(
        FakeHttpMessageHandler handler,
        string? defaultAccountId = null)
        => new(FakeHttpMessageHandler.CreateClient(handler), defaultAccountId);

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
    public void BuildEstimatePayload_UsesOnlyDocumentedFields()
    {
        var body = AssignmentResource.BuildEstimatePayload(new CreateAssignmentRequest
        {
            Signers =
            [
                new SignerRef { Id = "ignored", VerificationMethod = "Whatsapp", Step = 1 },
                new SignerRef(),
            ],
            Message = "ignored",
        });

        var signers = (List<Dictionary<string, object?>>)body["signers"]!;
        signers[0]["verification_method"].Should().Be("Whatsapp");
        signers[0].Should().NotContainKeys("id", "step");
        body.Should().NotContainKey("message");
        signers.Should().HaveCount(2);
    }

    [Fact]
    public void BuildEstimatePayload_SendsSignersAlongsideCollectEntries()
    {
        var body = AssignmentResource.BuildEstimatePayload(new CreateAssignmentRequest
        {
            Method = "collect",
            Signers = [new SignerRef { VerificationMethod = "DigitalCertificate" }],
            Entries = [new AssignmentEntry
            {
                PageId = "p1",
                Fields = [new AssignmentEntryField { SignerId = "s1", FieldId = "f1" }],
            }],
        });

        // collect is priced per signer too, so the channels must reach the API.
        var signers = (List<Dictionary<string, object?>>)body["signers"]!;
        signers[0]["verification_method"].Should().Be("DigitalCertificate");
        body.Should().ContainKey("entries");
    }

    [Theory]
    [InlineData("virtual")]
    [InlineData("collect")]
    public void BuildEstimatePayload_RequiresAtLeastOneSigner(string method)
    {
        // The API refuses a signer-less estimate in either mode.
        var act = () => AssignmentResource.BuildEstimatePayload(new CreateAssignmentRequest
        {
            Method = method,
            Entries = [new AssignmentEntry
            {
                PageId = "p1",
                Fields = [new AssignmentEntryField { SignerId = "s1", FieldId = "f1" }],
            }],
        });

        act.Should().Throw<ValidationException>().WithMessage("*At least one signer*");
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
    public async Task Create_SerializesDocumentedCollectDisplaySettings()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/documents/doc-1/assignments",
            FakeHttpMessageHandler.ApiOk(new { id = "assignment-1", method = "collect" }));

        await CreateResource(handler).CreateAsync("doc-1", new CreateAssignmentRequest
        {
            Method = "collect",
            Signers = ["s1"],
            Entries =
            [
                new AssignmentEntry
                {
                    PageId = "page-1",
                    Fields =
                    [
                        new AssignmentEntryField
                        {
                            SignerId = "s1",
                            FieldId = "field-1",
                            DisplaySettings = new DisplaySettings
                            {
                                Left = 69,
                                Top = 282,
                                Width = 421,
                                Height = 45.86,
                                FontSize = 22,
                                FontFamily = "Arial",
                                BackgroundColor = "#D5EBFF",
                            },
                        },
                    ],
                },
            ],
        });

        var settings = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0))
            .RootElement.GetProperty("entries")[0].GetProperty("fields")[0]
            .GetProperty("display_settings");
        settings.GetProperty("left").GetDouble().Should().Be(69);
        settings.GetProperty("top").GetDouble().Should().Be(282);
        settings.GetProperty("fontSize").GetDouble().Should().Be(22);
        settings.GetProperty("fontFamily").GetString().Should().Be("Arial");
        settings.GetProperty("backgroundColor").GetString().Should().Be("#D5EBFF");
        settings.TryGetProperty("x", out _).Should().BeFalse();
        settings.TryGetProperty("y", out _).Should().BeFalse();
    }

    [Fact]
    public void BuildPayload_RejectsInvalidCollectGeometry()
    {
        var act = () => AssignmentResource.BuildPayload(new CreateAssignmentRequest
        {
            Method = "collect",
            Signers = ["s1"],
            Entries =
            [
                new AssignmentEntry
                {
                    PageId = "page-1",
                    Fields =
                    [
                        new AssignmentEntryField
                        {
                            SignerId = "s1",
                            FieldId = "field-1",
                            DisplaySettings = new DisplaySettings
                            {
                                Left = 0,
                                Top = 0,
                                Width = 0,
                                Height = 10,
                                FontSize = 12,
                            },
                        },
                    ],
                },
            ],
        });

        act.Should().Throw<ValidationException>().WithMessage("*positive width*");
    }

    [Fact]
    public async Task List_SendsAccountOnlyWhenExplicitlyRequestedForCompatibility()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/assignments",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));

        await CreateResource(handler, "configured-account").ListAsync(accountId: "legacy-account");

        handler.Requests.Single().RequestUri!.Query.Should().Contain("accountId=legacy-account");
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
