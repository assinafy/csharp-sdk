using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class FieldResourceTests
{
    private static FieldResource CreateResource(FakeHttpMessageHandler handler, string? accountId = "acc")
        => new(FakeHttpMessageHandler.CreateClient(handler), accountId);

    [Fact]
    public async Task Create_PostsToAccountFields()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/fields",
            FakeHttpMessageHandler.ApiOk(new { id = "field-1", name = "CPF", type = "cpf", is_active = true }));

        var resource = CreateResource(handler);
        var result = await resource.CreateAsync(new CreateFieldDefinitionRequest
        {
            Name = "CPF",
            Type = "cpf",
        });

        result.Id.Should().Be("field-1");
    }

    [Fact]
    public async Task List_AddsIncludeFlags()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "include_standard=true",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));

        var resource = CreateResource(handler);
        await resource.ListAsync(new FieldListParams { IncludeStandard = true });

        handler.Requests.Should().Contain(r =>
            r.RequestUri!.Query.Contains("include_standard=true"));
    }

    [Fact]
    public async Task Validate_AddsSignerAccessCodeWhenProvided()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "signer-access-code=access",
            FakeHttpMessageHandler.ApiOk(new { type = "cpf", success = true, error_message = "" }));

        var resource = CreateResource(handler);
        var result = await resource.ValidateAsync(
            "field-1",
            new ValidateFieldValueRequest { Value = "400.676.228-36" },
            signerAccessCode: "access");

        result.Success.Should().BeTrue();
    }

    [Fact]
    public async Task ListTypes_CallsGlobalEndpoint()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/field-types",
            FakeHttpMessageHandler.ApiOk(new[] { new { type = "text", name = "Text" } }));

        var resource = CreateResource(handler);
        var result = await resource.ListTypesAsync();

        result.Should().ContainSingle(t => t.Type == "text");
    }
}
