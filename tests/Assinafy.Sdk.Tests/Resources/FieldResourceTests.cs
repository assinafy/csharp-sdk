using System.Text.Json;
using Assinafy.Sdk.Exceptions;
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
    public async Task Update_CanSendExplicitNullRegexAndPreserveLegacyExtensions()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/accounts/acc/fields/field-1",
            FakeHttpMessageHandler.ApiOk(new { id = "field-1", name = "Reference", type = "text" }));
        var resource = CreateResource(handler);

        await resource.UpdateAsync("field-1", new UpdateFieldDefinitionRequest
        {
            Name = "Reference",
            ClearRegex = true,
            IsActive = false,
            Type = "text",
            IsRequired = true,
        });

        var body = JsonDocument.Parse(handler.RequestBodies.Single());
        body.RootElement.GetProperty("regex").ValueKind.Should().Be(JsonValueKind.Null);
        body.RootElement.GetProperty("is_active").GetBoolean().Should().BeFalse();
        body.RootElement.GetProperty("type").GetString().Should().Be("text");
        body.RootElement.GetProperty("is_required").GetBoolean().Should().BeTrue();

        var invalid = () => resource.UpdateAsync("field-1", new UpdateFieldDefinitionRequest
        {
            Regex = ".+",
            ClearRegex = true,
        });
        await invalid.Should().ThrowAsync<ValidationException>();
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

    [Theory]
    [InlineData(false, "access")]
    [InlineData(true, "access")]
    [InlineData(false, null)]
    [InlineData(true, null)]
    public async Task Validation_UsesOnlyTheSelectedCredential(bool multiple, string? accessCode)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/validate",
            FakeHttpMessageHandler.ApiOk(new { type = "text", success = true }));
        handler.AddJsonResponse(HttpMethod.Post, "/validate-multiple",
            FakeHttpMessageHandler.ApiOk(new[] { new { field_id = "field-1", type = "text", success = true } }));
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        var resource = new FieldResource(http, "acc", request => request.Headers.Add("X-Api-Key", "key"));

        if (multiple)
            await resource.ValidateMultipleAsync([new ValidateFieldValueItem { FieldId = "field-1", Value = "value" }], accessCode);
        else
            await resource.ValidateAsync("field-1", new ValidateFieldValueRequest { Value = "value" }, accessCode);

        var sent = handler.Requests.Should().ContainSingle().Subject;
        sent.Method.Should().Be(HttpMethod.Post);
        sent.RequestUri!.AbsolutePath.Should().Be(multiple
            ? "/v1/accounts/acc/fields/validate-multiple"
            : "/v1/accounts/acc/fields/field-1/validate");
        sent.RequestUri.Query.Should().Be(accessCode is null ? "" : "?signer-access-code=access");
        sent.Headers.Contains("X-Api-Key").Should().Be(accessCode is null);
        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        var value = multiple ? body.RootElement[0] : body.RootElement;
        value.GetProperty("value").GetString().Should().Be("value");
        if (multiple) value.GetProperty("field_id").GetString().Should().Be("field-1");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task Validation_RejectsBlankAccessCodesBeforeSending(string accessCode)
    {
        var handler = new FakeHttpMessageHandler();
        var resource = CreateResource(handler);
        await ((Func<Task>)(() => resource.ValidateAsync("field-1", new ValidateFieldValueRequest { Value = "value" }, accessCode)))
            .Should().ThrowAsync<ValidationException>();
        await ((Func<Task>)(() => resource.ValidateMultipleAsync([], accessCode)))
            .Should().ThrowAsync<ValidationException>();
        handler.Requests.Should().BeEmpty();
    }
}
