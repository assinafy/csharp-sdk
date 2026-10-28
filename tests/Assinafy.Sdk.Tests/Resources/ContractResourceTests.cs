using System.Net;
using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class ContractResourceTests
{
    private const string Contract = """
        {"resource":"document_contract","document_id":"doc-1","status":"active",
         "counterparty_name":"Example","counterparty_taxpayer_id":"11222333000181",
         "value":120000.50,"currency":"BRL","start_date":"2026-11-01","end_date":"2027-10-31",
         "auto_renew":true,"renewal_term_months":12,"notice_period_days":30,"term":1,"owner_id":"owner",
         "metadata":{"crm_stage":"won"},"external_references":[{"system":"crm","id":"ref-1"}],
         "created_at":"2026-10-28T12:00:00Z","updated_at":"2026-10-28T12:00:00Z"}
        """;

    private const string Amendment = """
        {"document_id":"amendment-1","resource":"document_contract_amendment","document_name":"Amendment",
         "parent_document_id":"doc-1","relation_type":"amendment","number":null,"status":"pending",
         "document_status":"pending_signature","apply_error":null,"currency":"BRL",
         "terms":{"value":130000,"end_date":"2028-06-30"},
         "changes":{"value":{"from":120000.50,"to":130000},"term":{"from":1,"to":2}},
         "applied_at":null,"created_at":"2026-10-28T12:00:00Z","updated_at":"2026-10-28T13:00:00Z"}
        """;

    private static AssinafyClient Client(FakeHttpMessageHandler handler, string? accountId = "acc") =>
        new(new AssinafyClientOptions { ApiKey = "example-key", AccountId = accountId },
            FakeHttpMessageHandler.CreateClient(handler));

    private static void Respond(FakeHttpMessageHandler handler, HttpMethod method, string suffix, string data) =>
        handler.AddRawResponse(method, suffix, "{\"status\":200,\"message\":\"\",\"data\":" + data + "}");

    [Fact]
    public async Task List_ForwardsEveryFilterAndPreservesExpandedDocumentAndPagination()
    {
        var handler = new FakeHttpMessageHandler();
        var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(Contract)!;
        data["document"] = new { id = "doc-1", name = "Agreement", has_contract = true };
        handler.AddJsonResponse(HttpMethod.Get, "/contracts", new
        {
            status = 200,
            data = new[] { data },
        }, headers: new Dictionary<string, string>
        {
            ["X-Pagination-Current-Page"] = "2",
            ["X-Pagination-Per-Page"] = "5",
            ["X-Pagination-Total-Count"] = "7",
            ["X-Pagination-Page-Count"] = "2",
        });
        using var client = Client(handler);
        var filters = new Dictionary<string, string?>
        {
            ["status"] = "active,expired",
            ["counterparty"] = "A & B",
            ["search"] = "Contract #1",
            ["end_date_from"] = "2026-01-01",
            ["end_date_to"] = "2027-12-31",
            ["ending_within_days"] = "0",
            ["external_system"] = "crm",
            ["external_id"] = "id/1",
            ["sort"] = "-updated_at",
            ["expand"] = "document",
            ["page"] = "2",
            ["per-page"] = "5",
        };

        var result = await client.Contracts.ListAsync(filters, accountId: "override");

        result.Data.Should().ContainSingle().Which.Document!.Name.Should().Be("Agreement");
        result.Meta.Should().BeEquivalentTo(new PaginationMeta { CurrentPage = 2, PerPage = 5, Total = 7, LastPage = 2 });
        var request = handler.Requests.Single();
        request.Method.Should().Be(HttpMethod.Get);
        request.RequestUri!.AbsolutePath.Should().Be("/v1/accounts/override/contracts");
        foreach (var (key, value) in filters)
            request.RequestUri.Query.Should().Contain($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value!)}");
        request.Headers.GetValues("X-Api-Key").Should().ContainSingle().Which.Should().Be("example-key");
    }

    [Fact]
    public async Task Get_ReturnsTypedContractWithCompleteTerms()
    {
        var handler = new FakeHttpMessageHandler();
        Respond(handler, HttpMethod.Get, "/contract", Contract);
        using var client = Client(handler);
        var result = (await client.Contracts.GetAsync("doc-1")).Should().BeOfType<DocumentContract>().Subject;

        result.Value.Should().Be(120000.50m);
        result.CounterpartyTaxpayerId.Should().Be("11222333000181");
        result.Metadata!["crm_stage"].Should().Be("won");
        result.ExternalReferences.Single().Id.Should().Be("ref-1");
        result.Term.Should().Be(1);
        result.AutoRenew.Should().BeTrue();
        result.UpdatedAt.Should().Be("2026-10-28T12:00:00Z");
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be("/v1/accounts/acc/documents/doc-1/contract");
    }

    [Fact]
    public async Task Get_ReturnsTypedAmendmentWithDiscriminatorAfterOtherProperties()
    {
        var handler = new FakeHttpMessageHandler();
        Respond(handler, HttpMethod.Get, "/contract", Amendment);
        using var client = Client(handler);
        var result = (await client.Contracts.GetAsync("amendment-1")).Should().BeOfType<DocumentContractAmendment>().Subject;

        result.ParentDocumentId.Should().Be("doc-1");
        result.Terms!["value"].GetDecimal().Should().Be(130000m);
        result.Changes!["value"].From.GetDecimal().Should().Be(120000.50m);
        result.Changes["term"].To.GetInt32().Should().Be(2);
        result.AppliedAt.Should().BeNull();
    }

    [Fact]
    public async Task Get_AmendmentPreservesNullDeltaAndFieldChanges()
    {
        var handler = new FakeHttpMessageHandler();
        Respond(handler, HttpMethod.Get, "/contract", """
            {"resource":"document_contract_amendment","terms":null,
             "changes":{"value":{"from":null,"to":null}}}
            """);
        using var client = Client(handler);
        var result = (DocumentContractAmendment)await client.Contracts.GetAsync("amendment-1");
        result.Terms.Should().BeNull();
        result.Changes!["value"].From.ValueKind.Should().Be(JsonValueKind.Null);
        result.Changes["value"].To.ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public async Task Set_PreservesNullFalseZeroAndOmission()
    {
        var handler = new FakeHttpMessageHandler();
        Respond(handler, HttpMethod.Put, "/contract", Contract);
        using var client = Client(handler);
        var result = await client.Contracts.SetAsync("doc-1", new Dictionary<string, object?>
        {
            ["counterparty_name"] = null,
            ["auto_renew"] = false,
            ["notice_period_days"] = 0,
            ["external_references"] = Array.Empty<object>(),
            ["updated_at"] = "2026-10-28T12:00:00Z",
        });

        result.Should().BeOfType<DocumentContract>();
        handler.Requests.Single().Method.Should().Be(HttpMethod.Put);
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be("/v1/accounts/acc/documents/doc-1/contract");
        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        body.RootElement.GetProperty("counterparty_name").ValueKind.Should().Be(JsonValueKind.Null);
        body.RootElement.GetProperty("auto_renew").GetBoolean().Should().BeFalse();
        body.RootElement.GetProperty("notice_period_days").GetInt32().Should().Be(0);
        body.RootElement.GetProperty("external_references").GetArrayLength().Should().Be(0);
        body.RootElement.TryGetProperty("value", out _).Should().BeFalse();
        body.RootElement.GetProperty("updated_at").GetString().Should().Be("2026-10-28T12:00:00Z");
    }

    [Theory]
    [InlineData("amendment")]
    [InlineData("renewal")]
    [InlineData("termination")]
    public async Task Set_LinksInstrumentWithItsDelta(string relation)
    {
        var handler = new FakeHttpMessageHandler();
        Respond(handler, HttpMethod.Put, "/contract", Amendment);
        using var client = Client(handler);
        var result = await client.Contracts.SetAsync("amendment-1", new Dictionary<string, object?>
        {
            ["parent_document_id"] = "doc-1",
            ["relation_type"] = relation,
            ["end_date"] = "2028-06-30",
        });
        result.Should().BeOfType<DocumentContractAmendment>();
        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        body.RootElement.GetProperty("parent_document_id").GetString().Should().Be("doc-1");
        body.RootElement.GetProperty("relation_type").GetString().Should().Be(relation);
    }

    [Fact]
    public async Task Set_UnlinksPendingInstrumentWithExplicitNull()
    {
        var handler = new FakeHttpMessageHandler();
        Respond(handler, HttpMethod.Put, "/contract", Amendment);
        using var client = Client(handler);
        await client.Contracts.SetAsync("amendment-1", new Dictionary<string, object?> { ["parent_document_id"] = null });
        handler.RequestBodies.Single().Should().Be("{\"parent_document_id\":null}");
    }

    [Fact]
    public async Task Related_ReturnsRootAndTypedAmendments()
    {
        var handler = new FakeHttpMessageHandler();
        Respond(handler, HttpMethod.Get, "/related", Contract.TrimEnd()[..^1] + ",\"amendments\":[" + Amendment + "]}");
        using var client = Client(handler);
        var result = await client.Contracts.GetRelatedAsync("amendment-1");
        result.DocumentId.Should().Be("doc-1");
        result.Amendments.Should().ContainSingle().Which.ParentDocumentId.Should().Be("doc-1");
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be("/v1/accounts/acc/documents/amendment-1/contract/related");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Renew_PreservesTermOrDatePreconditionAndNullValue(bool byTerm)
    {
        var handler = new FakeHttpMessageHandler();
        Respond(handler, HttpMethod.Post, "/renew", Contract);
        using var client = Client(handler);
        var request = new Dictionary<string, object?> { ["value"] = null, ["updated_at"] = "2026-10-28T12:00:00Z" };
        if (byTerm) request["term"] = 1;
        else request["end_date"] = "2028-10-31";
        await client.Contracts.RenewAsync("doc-1", request);
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be("/v1/accounts/acc/documents/doc-1/contract/renew");
        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        body.RootElement.GetProperty("value").ValueKind.Should().Be(JsonValueKind.Null);
        body.RootElement.GetProperty(byTerm ? "term" : "end_date").ToString().Should().Be(byTerm ? "1" : "2028-10-31");
    }

    [Fact]
    public async Task Terminate_PostsReasonAndPrecondition()
    {
        var handler = new FakeHttpMessageHandler();
        Respond(handler, HttpMethod.Post, "/terminate", Contract);
        using var client = Client(handler);
        await client.Contracts.TerminateAsync("doc-1", new Dictionary<string, object?>
        { ["reason"] = "Agreement ended", ["updated_at"] = "2026-10-28T12:00:00Z" });
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be("/v1/accounts/acc/documents/doc-1/contract/terminate");
        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        body.RootElement.GetProperty("reason").GetString().Should().Be("Agreement ended");
    }

    [Theory]
    [InlineData(400)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(409)]
    [InlineData(429)]
    [InlineData(500)]
    public async Task Renew_NeverRetriesFailures(int status)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/renew", new { status, message = "Refused" }, (HttpStatusCode)status);
        using var client = Client(handler);
        var action = () => client.Contracts.RenewAsync("doc-1", new Dictionary<string, object?> { ["term"] = 1 });
        (await action.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(status);
        handler.Requests.Should().ContainSingle();
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"resource\":1}")]
    [InlineData("{\"resource\":\"unexpected\"}")]
    public async Task Get_RejectsInvalidUnionPayload(string payload)
    {
        var handler = new FakeHttpMessageHandler();
        Respond(handler, HttpMethod.Get, "/contract", payload);
        using var client = Client(handler);
        await ((Func<Task>)(() => client.Contracts.GetAsync("doc-1"))).Should().ThrowAsync<SerializationException>();
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Get_RejectsInvalidIdentifiers(string id)
    {
        var handler = new FakeHttpMessageHandler();
        using var client = Client(handler);
        await ((Func<Task>)(() => client.Contracts.GetAsync(id))).Should().ThrowAsync<ValidationException>();
        await ((Func<Task>)(() => client.Contracts.GetAsync("doc-1", id))).Should().ThrowAsync<ValidationException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Writes_RejectEmptyTermsAndUnprotectedRenewalBeforeTransport()
    {
        var handler = new FakeHttpMessageHandler();
        using var client = Client(handler);
        await ((Func<Task>)(() => client.Contracts.SetAsync("doc-1", new Dictionary<string, object?>())))
            .Should().ThrowAsync<ValidationException>();
        await ((Func<Task>)(() => client.Contracts.RenewAsync("doc-1", new Dictionary<string, object?> { ["end_date"] = null })))
            .Should().ThrowAsync<ValidationException>();
        handler.Requests.Should().BeEmpty();
    }
}
