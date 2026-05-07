using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class WebhookResourceTests
{
    private static WebhookResource CreateResource(FakeHttpMessageHandler handler, string? accountId = "acc")
        => new(FakeHttpMessageHandler.CreateClient(handler), accountId);

    [Fact]
    public async Task UpdateSubscription_PostsDocumentedShape()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/webhooks/subscriptions",
            FakeHttpMessageHandler.ApiOk(new
            {
                url = "https://example.com/webhook",
                email = "ops@example.com",
                is_active = true,
                events = new[] { "document_ready" },
            }));

        var resource = CreateResource(handler);
        await resource.UpdateSubscriptionAsync(new UpdateWebhookSubscriptionRequest
        {
            Url = "https://example.com/webhook",
            Email = "ops@example.com",
            IsActive = true,
            Events = ["document_ready"],
        });

        var body = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0));
        body.RootElement.GetProperty("events").EnumerateArray().Single().GetString()
            .Should().Be("document_ready");
        body.RootElement.GetProperty("is_active").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task UpdateSubscription_RequiresEvents()
    {
        var handler = new FakeHttpMessageHandler();
        var resource = CreateResource(handler);

        var act = () => resource.UpdateSubscriptionAsync(new UpdateWebhookSubscriptionRequest
        {
            Url = "https://example.com/webhook",
            Email = "ops@example.com",
            IsActive = true,
            Events = [],
        });

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ListEventTypes_CallsGlobalEndpoint()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/webhooks/event-types",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));

        var resource = CreateResource(handler);
        await resource.ListEventTypesAsync();

        handler.Requests.Should().Contain(r =>
            r.RequestUri!.PathAndQuery.Contains("/webhooks/event-types") &&
            r.Method == HttpMethod.Get);
    }

    [Fact]
    public async Task ListDispatches_PassesFiltersAndPaginationHeaders()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(
            HttpMethod.Get,
            "/webhooks",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()),
            headers: new Dictionary<string, string>
            {
                ["x-pagination-current-page"] = "1",
                ["x-pagination-per-page"] = "20",
                ["x-pagination-total-count"] = "2",
                ["x-pagination-page-count"] = "1",
            });

        var resource = CreateResource(handler);
        var result = await resource.ListDispatchesAsync(new ListDispatchesParams
        {
            Delivered = false,
            PerPage = 20,
        });

        handler.Requests.Should().Contain(r => r.RequestUri!.PathAndQuery.Contains("/accounts/acc/webhooks"));
        result.Meta!.CurrentPage.Should().Be(1);
        result.Meta.PerPage.Should().Be(20);
        result.Meta.Total.Should().Be(2);
        result.Meta.LastPage.Should().Be(1);
    }

    [Fact]
    public async Task RetryDispatch_RequiresDispatchId()
    {
        var handler = new FakeHttpMessageHandler();
        var resource = CreateResource(handler);

        var act = () => resource.RetryDispatchAsync("");

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Get_ReturnsNullWhen404()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/webhooks/subscriptions",
            JsonSerializer.Serialize(new { status = 404, message = "Not found", data = (object?)null }),
            System.Net.HttpStatusCode.OK);

        var resource = CreateResource(handler);
        var result = await resource.GetAsync();

        result.Should().BeNull();
    }
}
