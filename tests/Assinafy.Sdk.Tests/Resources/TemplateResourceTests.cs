using System.Text;
using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class TemplateResourceTests
{
    private static TemplateResource CreateResource(FakeHttpMessageHandler handler, string? accountId = "acc")
        => new(FakeHttpMessageHandler.CreateClient(handler), accountId);

    [Fact]
    public async Task List_CallsAccountTemplatesEndpoint()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/accounts/acc/templates",
            FakeHttpMessageHandler.ApiOk(new[]
            {
                new { id = "tmpl-1", name = "NDA Template", status = "ready", created_at = "2026-01-01" },
            }));

        var resource = CreateResource(handler);
        var result = await resource.ListAsync();

        result.Data.Should().HaveCount(1);
        result.Data[0].Id.Should().Be("tmpl-1");
        result.Data[0].Name.Should().Be("NDA Template");
    }

    [Fact]
    public async Task List_PassesQueryParams()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "search=NDA",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));

        var resource = CreateResource(handler);
        await resource.ListAsync(new Dictionary<string, string?> { ["search"] = "NDA" });

        handler.Requests.Should().Contain(r =>
            r.RequestUri!.Query.Contains("search=NDA"));
    }

    [Fact]
    public async Task Get_CallsAccountTemplateEndpoint()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/accounts/acc/templates/tmpl-1",
            FakeHttpMessageHandler.ApiOk(new
            {
                id = "tmpl-1",
                name = "NDA Template",
                status = "ready",
                created_at = "2026-01-01",
                roles = new[] { new { id = "role-1", name = "Signer" } },
            }));

        var resource = CreateResource(handler);
        var result = await resource.GetAsync("tmpl-1");

        result.Id.Should().Be("tmpl-1");
        result.Roles.Should().HaveCount(1);
        result.Roles[0].Id.Should().Be("role-1");
    }

    [Fact]
    public async Task Create_PostsOnePdfPartAndUsesNameAsFileName()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/templates",
            FakeHttpMessageHandler.ApiOk(new
            {
                resource = "template",
                id = "tmpl-1",
                name = "My Template.pdf",
                status = "Uploaded",
                pages = Array.Empty<object>(),
                roles = Array.Empty<object>(),
                tags = Array.Empty<object>(),
                created_at = "2026-01-01",
            }));

        var resource = CreateResource(handler);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("%PDF-1.4 test"));
        var result = await resource.CreateAsync(stream, "source.pdf", "My Template");

        result.Id.Should().Be("tmpl-1");
        result.Name.Should().Be("My Template.pdf");
        stream.CanRead.Should().BeTrue("the SDK must not dispose caller-owned streams");
        handler.Requests.Should().ContainSingle(r =>
            r.Method == HttpMethod.Post &&
            r.RequestUri!.AbsolutePath == "/v1/accounts/acc/templates");
        handler.RequestBodies.Should().ContainSingle(body =>
            body.Contains("name=file", StringComparison.Ordinal) &&
            body.Contains("filename=\"My Template.pdf\"", StringComparison.Ordinal) &&
            body.Contains("Content-Type: application/pdf", StringComparison.Ordinal) &&
            !body.Contains("name=name", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Create_ValidatesOnlyBytesRemainingInSeekableStream()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/templates",
            FakeHttpMessageHandler.ApiOk(new
            {
                id = "tmpl-1",
                name = "Template.pdf",
                status = "Uploaded",
                pages = Array.Empty<object>(),
                roles = Array.Empty<object>(),
                tags = Array.Empty<object>(),
                created_at = "2026-01-01",
            }));

        var resource = CreateResource(handler);
        using var stream = new MemoryStream(new byte[25 * 1024 * 1024 + 1]);
        stream.Position = stream.Length - 1;

        var result = await resource.CreateAsync(stream, "template.pdf");

        result.Id.Should().Be("tmpl-1");
        handler.Requests.Should().ContainSingle();
    }

    [Fact]
    public async Task Create_ThrowsSerializationExceptionWhenResponseHasNoTemplateId()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/templates",
            FakeHttpMessageHandler.ApiOk(new
            {
                id = "",
                name = "Template.pdf",
                status = "Uploaded",
                pages = Array.Empty<object>(),
                roles = Array.Empty<object>(),
                tags = Array.Empty<object>(),
                created_at = "2026-01-01",
            }));

        var resource = CreateResource(handler);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("pdf"));

        await ((Func<Task>)(() => resource.CreateAsync(stream, "template.pdf")))
            .Should().ThrowAsync<SerializationException>()
            .WithMessage("*no template ID*");
    }

    [Fact]
    public async Task UpdateDeleteAndDownloadPage_UseExpectedEndpointsAndPayloads()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/accounts/acc/templates/tmpl-1",
            FakeHttpMessageHandler.ApiOk(new
            {
                id = "tmpl-1",
                name = "NDA v2",
                message = "Please sign",
                status = "Ready",
                pages = Array.Empty<object>(),
                roles = Array.Empty<object>(),
                tags = Array.Empty<object>(),
                created_at = "2026-01-01",
            }));
        handler.AddJsonResponse(HttpMethod.Delete, "/accounts/acc/templates/tmpl-1",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));
        handler.AddRawResponse(HttpMethod.Get, "/accounts/acc/templates/tmpl-1/pages/page-1/download", "jpeg");

        var resource = CreateResource(handler);
        var updated = await resource.UpdateAsync(
            "tmpl-1",
            new UpdateTemplateRequest { Name = "NDA v2", Message = "Please sign" });
        await resource.DeleteAsync("tmpl-1");
        var page = await resource.DownloadPageAsync("tmpl-1", "page-1");

        updated.Name.Should().Be("NDA v2");
        page.Should().Equal(Encoding.UTF8.GetBytes("jpeg"));
        handler.Requests.Select(r => (r.Method, r.RequestUri!.AbsolutePath)).Should().Equal(
            (HttpMethod.Put, "/v1/accounts/acc/templates/tmpl-1"),
            (HttpMethod.Delete, "/v1/accounts/acc/templates/tmpl-1"),
            (HttpMethod.Get, "/v1/accounts/acc/templates/tmpl-1/pages/page-1/download"));

        using var body = JsonDocument.Parse(handler.RequestBodies[0]);
        body.RootElement.EnumerateObject().Select(property => property.Name).Should().Equal("name", "message");
        body.RootElement.GetProperty("name").GetString().Should().Be("NDA v2");
        body.RootElement.GetProperty("message").GetString().Should().Be("Please sign");
    }
}
