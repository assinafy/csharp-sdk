using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class EnvelopeResourceTests
{
    private const string Envelope = """
        {"resource":"envelope","id":"env","name":"Agreement","message":null,
         "documents":[{"id":"doc","position":1,"name":"Agreement.pdf","status":"pending_signature",
         "artifacts":{"original":"https://api.assinafy.com.br/v1/documents/doc/download/original"},
         "signers":[{"id":"signer","full_name":"Example Signer","email":"signer@example.com",
         "whatsapp_phone_number":null,"completed":false,"signed_at":null}]}],
         "counts":{"pending_signature":1},"signing_urls":[{"signer_id":"signer","url":"https://example.com/sign?secret=example","embedded":false}],
         "created_at":"2026-11-11T12:00:00Z"}
        """;

    private static AssinafyClient Client(FakeHttpMessageHandler handler) =>
        new(new AssinafyClientOptions { ApiKey = "example-key", AccountId = "acc" }, FakeHttpMessageHandler.CreateClient(handler));

    private static CreateEnvelopeRequest Request() => new()
    {
        Name = "Agreement",
        Signers = [new() { Id = "signer", VerificationMethod = SignerChannels.Email, NotificationMethods = ["Email"], Embedded = true }],
        Documents = [new() { Id = "doc", SignerIds = ["signer"], Method = "virtual", ExpiresAt = "2026-12-31T23:59:59Z", CopyReceiverIds = ["copy"] }],
        MaxTotalCredits = 0,
    };

    private static void Respond(FakeHttpMessageHandler handler, HttpMethod method, string path, string data = Envelope) =>
        handler.AddRawResponse(method, path, "{\"status\":200,\"data\":" + data + "}");

    [Fact]
    public async Task Create_ForwardsWholeBodyAndIdempotencyHeaderAndReadsEnvelope()
    {
        var handler = new FakeHttpMessageHandler();
        Respond(handler, HttpMethod.Post, "/envelopes");
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        using var client = new AssinafyClient(new AssinafyClientOptions { ApiKey = "example-key", AccountId = "acc" }, http);
        var request = Request();
        request.Message = "Please sign";

        var result = await client.Envelopes.CreateAsync(request, idempotencyKey: "case-Sensitive", accountId: "override");

        var sent = handler.Requests.Single();
        sent.Method.Should().Be(HttpMethod.Post);
        sent.RequestUri!.AbsolutePath.Should().Be("/v1/accounts/override/envelopes");
        sent.Headers.GetValues("Idempotency-Key").Should().Equal("case-Sensitive");
        sent.Headers.GetValues("X-Api-Key").Should().Equal("example-key");
        http.DefaultRequestHeaders.Contains("Idempotency-Key").Should().BeFalse();
        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        body.RootElement.GetProperty("name").GetString().Should().Be("Agreement");
        body.RootElement.GetProperty("message").GetString().Should().Be("Please sign");
        body.RootElement.GetProperty("max_total_credits").GetDecimal().Should().Be(0);
        body.RootElement.GetProperty("signers")[0].GetProperty("embedded").GetBoolean().Should().BeTrue();
        body.RootElement.GetProperty("signers")[0].TryGetProperty("step", out _).Should().BeFalse();
        body.RootElement.GetProperty("documents")[0].GetProperty("signer_ids")[0].GetString().Should().Be("signer");
        body.RootElement.GetProperty("documents")[0].GetProperty("copy_receiver_ids")[0].GetString().Should().Be("copy");
        body.RootElement.GetProperty("documents")[0].GetProperty("expires_at").GetString().Should().Be("2026-12-31T23:59:59Z");
        result.Counts!["pending_signature"].Should().Be(1);
        result.Documents[0].Position.Should().Be(1);
        result.Documents[0].Signers[0].SignedAt.Should().BeNull();
        result.SigningUrls.Should().ContainSingle();
        result.ToString().Should().NotContain("secret=example");
    }

    [Fact]
    public async Task Estimate_DoesNotApplyCeilingAndPreservesUnlimitedBalances()
    {
        var handler = new FakeHttpMessageHandler();
        Respond(handler, HttpMethod.Post, "/envelopes/estimate-cost", """
            {"documents":1,"credits":0,"total_credits":0,"document_balance":null,"credit_balance":null,
             "has_sufficient_resources":true,"needs_extra_document":false,"extra_document_cost":0,"breakdown":[],"blocking_reason":null,"message":null}
            """);
        using var client = Client(handler);
        var request = Request();
        request.MaxTotalCredits = -1;
        var estimate = await client.Envelopes.EstimateCostAsync(request);

        handler.Requests.Single().Method.Should().Be(HttpMethod.Post);
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be("/v1/accounts/acc/envelopes/estimate-cost");
        handler.Requests.Single().Headers.Contains("Idempotency-Key").Should().BeFalse();
        estimate.AvailableCreditBalance.Should().BeNull();
        estimate.AvailableDocumentBalance.Should().BeNull();
        estimate.CreditBalance.Should().Be(0);
        estimate.DocumentBalance.Should().Be(0);
        estimate.HasSufficientResources.Should().BeTrue();
    }

    [Fact]
    public async Task ListAndActivities_PreserveFiltersAndPaginationAndDocumentIdentity()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/envelopes", new { status = 200, data = new[] { new { id = "env", counts = (object?)null } } },
            headers: new Dictionary<string, string> { ["X-Pagination-Current-Page"] = "2", ["X-Pagination-Page-Count"] = "3" });
        Respond(handler, HttpMethod.Get, "/activities", """
            [{"id":7,"event":"signature_requested","message":null,"payload":[],"origin":{"ip":null},
              "created_at":"2026-11-11T12:00:00Z","document_id":"doc","document_name":"Agreement.pdf"}]
            """);
        using var client = Client(handler);
        var list = await client.Envelopes.ListAsync(new Dictionary<string, string?> { ["search"] = "A & B", ["status"] = "open", ["page"] = "2", ["per-page"] = "1" });
        list.Meta!.CurrentPage.Should().Be(2);
        list.Data[0].Counts.Should().BeNull();
        handler.Requests[0].RequestUri!.Query.Should().Contain("search=A%20%26%20B").And.Contain("status=open").And.Contain("page=2").And.Contain("per-page=1");
        var activities = await client.Envelopes.ActivitiesAsync("env", new Dictionary<string, string?> { ["sort"] = "id", ["page"] = "2" });
        activities.Data[0].DocumentId.Should().Be("doc");
        activities.Data[0].DocumentName.Should().Be("Agreement.pdf");
        activities.Data[0].Payload!.Value.ValueKind.Should().Be(JsonValueKind.Array);
        handler.Requests[1].RequestUri!.AbsolutePath.Should().Be("/v1/accounts/acc/envelopes/env/activities");
        handler.Requests[1].RequestUri!.Query.Should().Contain("sort=id").And.Contain("page=2");
    }

    [Fact]
    public async Task GetUpdateDeleteAndRemove_UseExactMethodsAndPreserveExplicitNull()
    {
        var handler = new FakeHttpMessageHandler();
        Respond(handler, HttpMethod.Get, "/envelopes/");
        Respond(handler, HttpMethod.Patch, "/envelopes/");
        Respond(handler, HttpMethod.Delete, "/envelopes/", "[]");
        Respond(handler, HttpMethod.Delete, "/documents/doc", Envelope);
        using var client = Client(handler);
        (await client.Envelopes.GetAsync("env/1")).Id.Should().Be("env");
        await client.Envelopes.UpdateAsync("env", new Dictionary<string, object?> { ["message"] = null, ["document_ids"] = new[] { "doc" } });
        (await client.Envelopes.RemoveDocumentAsync("env", "doc")).Id.Should().Be("env");
        await client.Envelopes.DeleteAsync("env");

        handler.Requests.Select(r => r.Method).Should().Equal(HttpMethod.Get, HttpMethod.Patch, HttpMethod.Delete, HttpMethod.Delete);
        handler.Requests[0].RequestUri!.AbsolutePath.Should().Be("/v1/accounts/acc/envelopes/env%2F1");
        handler.Requests[2].RequestUri!.AbsolutePath.Should().Be("/v1/accounts/acc/envelopes/env/documents/doc");
        handler.Requests[3].RequestUri!.AbsolutePath.Should().Be("/v1/accounts/acc/envelopes/env");
        using var body = JsonDocument.Parse(handler.RequestBodies[1]);
        body.RootElement.GetProperty("message").ValueKind.Should().Be(JsonValueKind.Null);
        body.RootElement.GetProperty("document_ids")[0].GetString().Should().Be("doc");
        body.RootElement.TryGetProperty("name", out _).Should().BeFalse();
    }

    [Fact]
    public async Task DownloadCancelResendAndAdd_UseTheirDistinctResponseShapes()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/download/original", "PK-example");
        Respond(handler, HttpMethod.Put, "/cancel", "[{\"id\":\"doc\",\"status\":\"rejected_by_user\",\"cancelled\":true}]");
        Respond(handler, HttpMethod.Put, "/resend", "{\"is_sent\":true,\"envelope_id\":\"env\",\"signer_id\":\"signer\"}");
        Respond(handler, HttpMethod.Post, "/documents");
        using var client = Client(handler);
        (await client.Envelopes.DownloadAsync("env", "original")).Should().Equal("PK-example"u8.ToArray());
        (await client.Envelopes.CancelAsync("env"))[0].Cancelled.Should().BeTrue();
        (await client.Envelopes.ResendNotificationAsync("env", "signer")).IsSent.Should().BeTrue();
        (await client.Envelopes.AddDocumentAsync("env", Request().Documents[0], maxTotalCredits: 0)).Id.Should().Be("env");

        handler.Requests.Select(r => r.Method).Should().Equal(HttpMethod.Get, HttpMethod.Put, HttpMethod.Put, HttpMethod.Post);
        handler.Requests.Select(r => r.RequestUri!.AbsolutePath).Should().Equal(
            "/v1/accounts/acc/envelopes/env/download/original", "/v1/accounts/acc/envelopes/env/cancel",
            "/v1/accounts/acc/envelopes/env/signers/signer/resend", "/v1/accounts/acc/envelopes/env/documents");
        using var body = JsonDocument.Parse(handler.RequestBodies[3]);
        body.RootElement.GetProperty("id").GetString().Should().Be("doc");
        body.RootElement.GetProperty("max_total_credits").GetDecimal().Should().Be(0);
        body.RootElement.GetProperty("signer_ids")[0].GetString().Should().Be("signer");
    }

    [Fact]
    public async Task InvalidInput_StopsBeforeHttp()
    {
        var handler = new FakeHttpMessageHandler();
        using var client = Client(handler);
        var request = Request();
        request.MaxTotalCredits = -1;
        await Assert.ThrowsAsync<ValidationException>(() => client.Envelopes.CreateAsync(request));
        request = Request(); request.Signers = [];
        await Assert.ThrowsAsync<ValidationException>(() => client.Envelopes.CreateAsync(request));
        request = Request(); request.Documents = [new() { Id = "doc", SignerIds = [" "] }];
        await Assert.ThrowsAsync<ValidationException>(() => client.Envelopes.CreateAsync(request));
        await Assert.ThrowsAsync<ValidationException>(() => client.Envelopes.UpdateAsync("env", new Dictionary<string, object?>()));
        await Assert.ThrowsAsync<ValidationException>(() => client.Envelopes.GetAsync(".."));
        await Assert.ThrowsAsync<ValidationException>(() => client.Envelopes.AddDocumentAsync("env", Request().Documents[0], -1));
        handler.Requests.Should().BeEmpty();
    }
    [Theory]
    [InlineData(" ")]
    [InlineData("key\r\nX-Extra: bad")]
    public async Task InvalidIdempotencyKey_IsRejectedBeforeHttp(string key)
    {
        var handler = new FakeHttpMessageHandler();
        using var client = Client(handler);
        await Assert.ThrowsAsync<ValidationException>(() => client.Envelopes.CreateAsync(Request(), key));
        handler.Requests.Should().BeEmpty();
    }

}
