using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class CreationSafetyTests
{
    [Fact]
    public async Task Assignment_PreservesZeroCeilingAndEmbeddedFlag()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/documents/doc/assignments", FakeHttpMessageHandler.ApiOk(new { id = "assignment" }));
        var resource = new AssignmentResource(FakeHttpMessageHandler.CreateClient(handler));
        await resource.CreateAsync("doc", new CreateAssignmentRequest
        {
            MaxTotalCredits = 0,
            Signers = [new SignerRef { Id = "signer", Embedded = true }],
        });
        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        body.RootElement.GetProperty("max_total_credits").GetDecimal().Should().Be(0);
        body.RootElement.GetProperty("signers")[0].GetProperty("embedded").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Template_UsesPerRequestIdempotencyAndCeiling()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/templates/template/documents", FakeHttpMessageHandler.ApiOk(new { id = "document" }));
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        var resource = new DocumentResource(http, "acc");
        await resource.CreateFromTemplateAsync("template", [new TemplateSigner { RoleId = "role", Id = "signer", Embedded = false }],
            new CreateDocumentFromTemplateOptions { IdempotencyKey = "case-Sensitive", MaxTotalCredits = 0 });
        await resource.CreateFromTemplateAsync("template", [new TemplateSigner { RoleId = "role", Id = "signer" }]);
        handler.Requests[0].Headers.GetValues("Idempotency-Key").Should().ContainSingle().Which.Should().Be("case-Sensitive");
        handler.Requests[1].Headers.Contains("Idempotency-Key").Should().BeFalse();
        http.DefaultRequestHeaders.Contains("Idempotency-Key").Should().BeFalse();
        using var body = JsonDocument.Parse(handler.RequestBodies[0]);
        body.RootElement.GetProperty("max_total_credits").GetDecimal().Should().Be(0);
        body.RootElement.GetProperty("signers")[0].GetProperty("embedded").GetBoolean().Should().BeFalse();
        body.RootElement.TryGetProperty("idempotency_key", out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("key\r\nX-Injected: yes")]
    public async Task Template_RejectsInvalidIdempotencyBeforeTransport(string key)
    {
        var handler = new FakeHttpMessageHandler();
        var resource = new DocumentResource(FakeHttpMessageHandler.CreateClient(handler), "acc");
        Func<Task> call = () => resource.CreateFromTemplateAsync("template", [new TemplateSigner { RoleId = "role", Id = "signer" }],
            new CreateDocumentFromTemplateOptions { IdempotencyKey = key });
        await call.Should().ThrowAsync<ValidationException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task NegativeCreditCeiling_FailsBeforeTransport()
    {
        var handler = new FakeHttpMessageHandler();
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        var assignments = new AssignmentResource(http);
        var documents = new DocumentResource(http, "acc");
        Func<Task> assignment = () => assignments.CreateAsync("doc", new CreateAssignmentRequest { SignerIds = ["signer"], MaxTotalCredits = -1 });
        Func<Task> template = () => documents.CreateFromTemplateAsync("template", [new TemplateSigner { RoleId = "role", Id = "signer" }],
            new CreateDocumentFromTemplateOptions { MaxTotalCredits = -1 });
        await assignment.Should().ThrowAsync<ValidationException>();
        await template.Should().ThrowAsync<ValidationException>();
        handler.Requests.Should().BeEmpty();
    }
}
