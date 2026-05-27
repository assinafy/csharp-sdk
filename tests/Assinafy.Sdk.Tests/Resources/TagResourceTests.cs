using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class TagResourceTests
{
    private static TagResource CreateResource(FakeHttpMessageHandler handler, string? accountId = "acc")
        => new(FakeHttpMessageHandler.CreateClient(handler), accountId);

    private static object Tag(string id, string name, string? color = null) =>
        new { resource = "tag", id, name, color, created_at = "2026-01-01T00:00:00Z", updated_at = "2026-01-01T00:00:00Z" };

    [Fact]
    public async Task List_PassesSearchAndReturnsTags()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/accounts/acc/tags",
            FakeHttpMessageHandler.ApiOk(new[] { Tag("t1", "Contracts", "3366FF") }));

        var resource = CreateResource(handler);
        var result = await resource.ListAsync("contr");

        result.Should().ContainSingle().Which.Name.Should().Be("Contracts");
        handler.Requests.Should().Contain(r =>
            r.RequestUri!.PathAndQuery.Contains("/accounts/acc/tags") &&
            r.RequestUri.Query.Contains("search=contr"));
    }

    [Fact]
    public async Task Create_PostsNameAndColor()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/tags",
            FakeHttpMessageHandler.ApiOk(Tag("t1", "Urgent", "FF0000")));

        var resource = CreateResource(handler);
        var result = await resource.CreateAsync(new CreateTagRequest { Name = "Urgent", Color = "FF0000" });

        result.Id.Should().Be("t1");
        var body = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0));
        body.RootElement.GetProperty("name").GetString().Should().Be("Urgent");
        body.RootElement.GetProperty("color").GetString().Should().Be("FF0000");
    }

    [Fact]
    public async Task Create_RequiresName()
    {
        var handler = new FakeHttpMessageHandler();
        var resource = CreateResource(handler);

        var act = () => resource.CreateAsync(new CreateTagRequest { Name = "" });

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Create_ThrowsWhenNoAccountId()
    {
        var handler = new FakeHttpMessageHandler();
        var resource = CreateResource(handler, accountId: null);

        var act = () => resource.CreateAsync(new CreateTagRequest { Name = "Urgent" });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Update_PutsToTagPath()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/accounts/acc/tags/t1",
            FakeHttpMessageHandler.ApiOk(Tag("t1", "Renamed", "00FF00")));

        var resource = CreateResource(handler);
        var result = await resource.UpdateAsync("t1", new UpdateTagRequest { Name = "Renamed", Color = "00FF00" });

        result.Name.Should().Be("Renamed");
        handler.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Put && r.RequestUri!.PathAndQuery.Contains("/accounts/acc/tags/t1"));
    }

    [Fact]
    public async Task Delete_AddsForceQueryWhenRequested()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Delete, "/accounts/acc/tags/t1",
            FakeHttpMessageHandler.ApiOk(new { deleted = true }));

        var resource = CreateResource(handler);
        await resource.DeleteAsync("t1", force: true);

        handler.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Delete &&
            r.RequestUri!.PathAndQuery.Contains("/accounts/acc/tags/t1") &&
            r.RequestUri.Query.Contains("force=true"));
    }

    [Fact]
    public async Task Delete_OmitsForceByDefault()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Delete, "/accounts/acc/tags/t1",
            FakeHttpMessageHandler.ApiOk(new { deleted = true }));

        var resource = CreateResource(handler);
        await resource.DeleteAsync("t1");

        handler.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Delete && !r.RequestUri!.Query.Contains("force"));
    }

    [Fact]
    public async Task Delete_RequiresTagId()
    {
        var handler = new FakeHttpMessageHandler();
        var resource = CreateResource(handler);

        var act = () => resource.DeleteAsync("");

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ListForDocument_CallsDocumentTagsPath()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/accounts/acc/documents/doc-1/tags",
            FakeHttpMessageHandler.ApiOk(new[] { Tag("t1", "Contracts") }));

        var resource = CreateResource(handler);
        var result = await resource.ListForDocumentAsync("doc-1");

        result.Should().ContainSingle();
        handler.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Get &&
            r.RequestUri!.PathAndQuery.Contains("/accounts/acc/documents/doc-1/tags"));
    }

    [Fact]
    public async Task AddToDocument_PostsTagsArray()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/documents/doc-1/tags",
            FakeHttpMessageHandler.ApiOk(new[] { Tag("t1", "Contracts") }));

        var resource = CreateResource(handler);
        await resource.AddToDocumentAsync("doc-1", ["Contracts"]);

        var body = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0));
        body.RootElement.GetProperty("tags").EnumerateArray().Single().GetString().Should().Be("Contracts");
        handler.Requests.Should().Contain(r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task SetForDocument_PutsTagsArray()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/accounts/acc/documents/doc-1/tags",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));

        var resource = CreateResource(handler);
        var result = await resource.SetForDocumentAsync("doc-1", []);

        result.Should().BeEmpty();
        var body = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0));
        body.RootElement.GetProperty("tags").GetArrayLength().Should().Be(0);
        handler.Requests.Should().Contain(r => r.Method == HttpMethod.Put);
    }

    [Fact]
    public async Task RemoveFromDocument_DeletesSpecificTag()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Delete, "/accounts/acc/documents/doc-1/tags/t1",
            FakeHttpMessageHandler.ApiOk(new { detached = true }));

        var resource = CreateResource(handler);
        await resource.RemoveFromDocumentAsync("doc-1", "t1");

        handler.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Delete &&
            r.RequestUri!.PathAndQuery.Contains("/accounts/acc/documents/doc-1/tags/t1"));
    }

    [Fact]
    public async Task RemoveFromDocument_RequiresIds()
    {
        var handler = new FakeHttpMessageHandler();
        var resource = CreateResource(handler);

        await ((Func<Task>)(() => resource.RemoveFromDocumentAsync("", "t1")))
            .Should().ThrowAsync<ValidationException>();
        await ((Func<Task>)(() => resource.RemoveFromDocumentAsync("doc-1", "")))
            .Should().ThrowAsync<ValidationException>();
    }
}
