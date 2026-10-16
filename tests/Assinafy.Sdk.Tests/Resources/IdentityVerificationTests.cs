using System.Net;
using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class IdentityVerificationTests
{
    private const string ReviewPath = "/v1/documents/doc%2F1/assignments/assignment%3F1/signers/signer%231/identity-verification";

    private static readonly object Review = new
    {
        id = "verification",
        status = "InReview",
        provider = "AssinafyVerify",
        verification_method = "IdDocumentFaceMatchBasic",
        reason_codes = new[] { new { code = "NAME_MISMATCH", description = "Names differ" } },
        evidence_available = true,
        expected = new { name = "Signer Name", cpf = "52998224725" },
        document = new { type = "CNH", name = "SIGNER NAME", cpf = "52998224725" },
        matches = new { cpf = true, name = 0.5217, birth_date = (bool?)null },
        checks = new[] { new { name = "name", result = "review", score = 0.5217, reasons = new[] { "NAME_MISMATCH" } } },
        images = new[] { "document_front", "document_back", "document_pdf", "face" },
        ruleset_version = "2026-10-12.2",
        evidence_sha256 = "example-hash",
        reviewed_by = new { id = "user", name = "Reviewer" },
        reviewed_at = "2026-10-13T21:05:00Z",
        review_note = "Checked",
        completed_at = "2026-10-13T21:05:00Z",
    };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SignerIdentityVerification_UsesAccessCodeOnlyAndPreservesCaptureUrl(bool useToken)
    {
        const string captureUrl = "https://verify.example.com/c/session#t=example-token";
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/signers/identity-verification", FakeHttpMessageHandler.ApiOk(new
        {
            can_start = false,
            status = "Pending",
            url = captureUrl,
            expires_at = "2026-10-17T12:00:00Z",
            completed_at = (string?)null,
        }));
        handler.AddJsonResponse(HttpMethod.Post, "/signers/identity-verification", FakeHttpMessageHandler.ApiOk(new
        {
            id = "session",
            url = captureUrl,
            expires_at = (string?)null,
        }));
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        using var client = new AssinafyClient(new AssinafyClientOptions
        {
            ApiKey = useToken ? null : "workspace-key",
            Token = useToken ? "workspace-token" : null,
        }, http);

        var status = await client.Signing.GetIdentityVerificationAsync("code &1");
        var session = await client.Signing.StartIdentityVerificationAsync("code &1", consent: true);

        status.CanStart.Should().BeFalse();
        status.Status.Should().Be("Pending");
        status.Url.Should().Be(captureUrl);
        status.ExpiresAt.Should().Be("2026-10-17T12:00:00Z");
        status.CompletedAt.Should().BeNull();
        session.Id.Should().Be("session");
        session.Url.Should().Be(captureUrl);
        session.ExpiresAt.Should().BeNull();
        handler.Requests.Select(r => r.Method).Should().Equal(HttpMethod.Get, HttpMethod.Post);
        foreach (var request in handler.Requests)
        {
            request.RequestUri!.PathAndQuery.Should().Be("/v1/signers/identity-verification?signer-access-code=code%20%261");
            request.Headers.Contains("X-Api-Key").Should().BeFalse();
            request.Headers.Authorization.Should().BeNull();
        }
        handler.RequestBodies.Should().Equal("", "{\"consent\":true}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task SignerIdentityVerification_RejectsBlankAccessCode(string? code)
    {
        var handler = new FakeHttpMessageHandler();
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        using var client = new AssinafyClient(new AssinafyClientOptions { ApiKey = "key" }, http);

        await ((Func<Task>)(() => client.Signing.GetIdentityVerificationAsync(code!))).Should().ThrowAsync<ValidationException>();
        await ((Func<Task>)(() => client.Signing.StartIdentityVerificationAsync(code!, true))).Should().ThrowAsync<ValidationException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task StartIdentityVerification_RequiresExplicitConsentBeforeSending()
    {
        var handler = new FakeHttpMessageHandler();
        var signing = new SigningResource(FakeHttpMessageHandler.CreateClient(handler));

        await ((Func<Task>)(() => signing.StartIdentityVerificationAsync("code", false)))
            .Should().ThrowAsync<ValidationException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task GetIdentityReview_AuthenticatesWorkspaceEscapesIdsAndReadsEntireSummary()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, ReviewPath, FakeHttpMessageHandler.ApiOk(Review));
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        using var client = new AssinafyClient(new AssinafyClientOptions { ApiKey = "key" }, http);

        var result = await client.Assignments.GetIdentityReviewAsync("doc/1", "assignment?1", "signer#1");

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Get);
        request.RequestUri!.PathAndQuery.Should().Be(ReviewPath);
        request.Headers.GetValues("X-Api-Key").Should().Equal("key");
        result.Id.Should().Be("verification");
        result.Status.Should().Be("InReview");
        result.Provider.Should().Be("AssinafyVerify");
        result.VerificationMethod.Should().Be(SignerChannels.IdDocumentFaceMatchBasic);
        result.ReasonCodes.Should().ContainSingle().Which.Description.Should().Be("Names differ");
        result.Expected!.Name.Should().Be("Signer Name");
        result.Expected.Cpf.Should().Be("52998224725");
        result.Document!.Type.Should().Be("CNH");
        result.Document.Name.Should().Be("SIGNER NAME");
        result.Matches!.Cpf.Should().BeTrue();
        result.Matches.Name.Should().Be(0.5217);
        result.Matches.BirthDate.Should().BeNull();
        result.Checks.Should().ContainSingle().Which.Reasons.Should().Equal("NAME_MISMATCH");
        result.Checks[0].Score.Should().Be(0.5217);
        result.Images.Should().Equal("document_front", "document_back", "document_pdf", "face");
        result.EvidenceAvailable.Should().BeTrue();
        result.RulesetVersion.Should().Be("2026-10-12.2");
        result.EvidenceSha256.Should().Be("example-hash");
        result.ReviewedBy!.Id.Should().Be("user");
        result.ReviewedAt.Should().Be("2026-10-13T21:05:00Z");
        result.ReviewNote.Should().Be("Checked");
        result.CompletedAt.Should().Be("2026-10-13T21:05:00Z");
    }

    [Theory]
    [InlineData("document_front")]
    [InlineData("document_back")]
    [InlineData("document_pdf")]
    [InlineData("face")]
    public async Task DownloadIdentityReviewImage_ReturnsBinaryFromEscapedPath(string name)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, $"{ReviewPath}/images/{name}", "EVIDENCE");
        var assignments = new AssignmentResource(FakeHttpMessageHandler.CreateClient(handler));

        (await assignments.DownloadIdentityReviewImageAsync("doc/1", "assignment?1", "signer#1", name))
            .Should().Equal("EVIDENCE"u8.ToArray());
        handler.Requests.Single().RequestUri!.PathAndQuery.Should().Be($"{ReviewPath}/images/{name}");
    }

    [Theory]
    [InlineData("Approved")]
    [InlineData("Rejected")]
    public async Task DecideIdentityReview_PostsExactDecisionAndPrivateNote(string decision)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, $"{ReviewPath}/review", FakeHttpMessageHandler.ApiOk(Review));
        var assignments = new AssignmentResource(FakeHttpMessageHandler.CreateClient(handler));

        await assignments.DecideIdentityReviewAsync("doc/1", "assignment?1", "signer#1", new()
        {
            Decision = decision,
            Note = "Private note",
        });

        handler.Requests.Single().RequestUri!.PathAndQuery.Should().Be($"{ReviewPath}/review");
        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        body.RootElement.GetProperty("decision").GetString().Should().Be(decision);
        body.RootElement.GetProperty("note").GetString().Should().Be("Private note");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("approved")]
    [InlineData("Declined")]
    public async Task DecideIdentityReview_RejectsInvalidDecisionBeforeSending(string? decision)
    {
        var handler = new FakeHttpMessageHandler();
        var assignments = new AssignmentResource(FakeHttpMessageHandler.CreateClient(handler));
        await ((Func<Task>)(() => assignments.DecideIdentityReviewAsync("doc", "a", "s", new() { Decision = decision! })))
            .Should().ThrowAsync<ValidationException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DecideIdentityReview_RejectsOverlongNoteBeforeSending()
    {
        var handler = new FakeHttpMessageHandler();
        var assignments = new AssignmentResource(FakeHttpMessageHandler.CreateClient(handler));
        await ((Func<Task>)(() => assignments.DecideIdentityReviewAsync("doc", "a", "s", new()
        {
            Decision = "Approved",
            Note = new string('x', 2001),
        }))).Should().ThrowAsync<ValidationException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task DecideIdentityReview_CountsUnicodeCharactersAndOmitsUnsetNote()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/identity-verification/review", FakeHttpMessageHandler.ApiOk(Review));
        var assignments = new AssignmentResource(FakeHttpMessageHandler.CreateClient(handler));
        var note = string.Concat(Enumerable.Repeat("\U0001F600", 2000));

        await assignments.DecideIdentityReviewAsync("d", "a", "s", new() { Decision = "Approved", Note = note });
        await assignments.DecideIdentityReviewAsync("d", "a", "s", new() { Decision = "Approved" });

        using var body = JsonDocument.Parse(handler.RequestBodies[0]);
        body.RootElement.GetProperty("note").GetString().Should().Be(note);
        handler.RequestBodies[1].Should().Be("{\"decision\":\"Approved\"}");
    }

    [Fact]
    public async Task IdentityVerification_MapsApiFailureWithoutRetrying()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/signers/identity-verification",
            new { status = 400, message = "Session already exists", data = (object?)null }, HttpStatusCode.BadRequest);
        var signing = new SigningResource(FakeHttpMessageHandler.CreateClient(handler));

        (await ((Func<Task>)(() => signing.StartIdentityVerificationAsync("code", true)))
            .Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(400);
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task WaitUntilReady_ReturnsDocumentAwaitingIdentityReview()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/doc",
            FakeHttpMessageHandler.ApiOk(new { id = "doc", status = "pending_identity_review" }));
        var documents = new DocumentResource(FakeHttpMessageHandler.CreateClient(handler));

        (await documents.WaitUntilReadyAsync("doc")).Status.Should().Be("pending_identity_review");
        handler.Requests.Should().ContainSingle();
    }
}
