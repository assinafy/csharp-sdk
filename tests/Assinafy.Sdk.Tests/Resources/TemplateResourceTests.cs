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
}
