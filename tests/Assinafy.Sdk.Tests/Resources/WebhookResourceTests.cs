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
    public async Task Get_PropagatesApi404()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/webhooks/subscriptions",
            JsonSerializer.Serialize(new { status = 404, message = "Not found", data = (object?)null }),
            System.Net.HttpStatusCode.OK);

        var resource = CreateResource(handler);
        var act = () => resource.GetAsync();

        await act.Should().ThrowAsync<ApiException>().Where(exception => exception.StatusCode == 404);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RotateEndpointSecret_SendsOverlapChoiceAndReadsExpiry(bool keepPrevious)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/secret/rotate", FakeHttpMessageHandler.ApiOk(new
        {
            secret = "whsec_ZXhhbXBsZQ==",
            previous_secret_expires_at = keepPrevious ? "2026-11-12T15:00:00Z" : null,
        }));
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        using var client = new AssinafyClient(new AssinafyClientOptions { Token = "oauth-token", AccountId = "default" }, http);

        var result = await client.Webhooks.RotateEndpointSecretWithOptionsAsync("ep/1", keepPrevious, "acc?1");

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.PathAndQuery.Should().Be("/v1/accounts/acc%3F1/webhooks/endpoints/ep%2F1/secret/rotate");
        request.Headers.Authorization!.Parameter.Should().Be("oauth-token");
        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        body.RootElement.EnumerateObject().Should().ContainSingle();
        body.RootElement.GetProperty("keep_previous").GetBoolean().Should().Be(keepPrevious);
        result.Secret.Should().Be("whsec_ZXhhbXBsZQ==");
        result.PreviousSecretExpiresAt.Should().Be(keepPrevious ? "2026-11-12T15:00:00Z" : null);
        result.ToString().Should().NotContain(result.Secret);
    }

    [Fact]
    public async Task RotateEndpointSecret_ExistingOverloadRemainsBodyless()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/secret/rotate", FakeHttpMessageHandler.ApiOk(new { secret = "new-secret" }));

        var result = await CreateResource(handler).RotateEndpointSecretAsync("ep", null);

        handler.RequestBodies.Should().Equal(string.Empty);
        result.PreviousSecretExpiresAt.Should().BeNull();
    }

    [Theory]
    [InlineData(184L)]
    [InlineData(null)]
    public async Task TestEndpoint_PostsToEscapedPathAndReadsDelivery(long? durationMs)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/test", FakeHttpMessageHandler.ApiOk(new
        {
            id = "dispatch",
            @event = "webhook_test",
            kind = "manual",
            delivered = false,
            will_retry = false,
            duration_ms = durationMs,
            http_status = 503,
            error = "Receiver unavailable",
        }));
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        using var client = new AssinafyClient(new AssinafyClientOptions { ApiKey = "workspace-key", AccountId = "default" }, http);

        var result = await client.Webhooks.TestEndpointAsync("ep/1", "acc?1");

        var request = handler.Requests.Should().ContainSingle().Subject;
        request.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.PathAndQuery.Should().Be("/v1/accounts/acc%3F1/webhooks/endpoints/ep%2F1/test");
        request.Headers.GetValues("X-Api-Key").Should().Equal("workspace-key");
        handler.RequestBodies.Should().Equal(string.Empty);
        result.Event.Should().Be("webhook_test");
        result.Kind.Should().Be("manual");
        result.Delivered.Should().BeFalse();
        result.WillRetry.Should().BeFalse();
        result.DurationMs.Should().Be(durationMs);
        result.HttpStatus.Should().Be(503);
        result.Error.Should().Be("Receiver unavailable");
    }

    [Theory]
    [InlineData("")]
    [InlineData("..")]
    public async Task TestAndRotation_RejectInvalidIdsBeforeSending(string endpointId)
    {
        var handler = new FakeHttpMessageHandler();
        var resource = CreateResource(handler);

        await ((Func<Task>)(() => resource.TestEndpointAsync(endpointId))).Should().ThrowAsync<ValidationException>();
        await ((Func<Task>)(() => resource.RotateEndpointSecretWithOptionsAsync(endpointId, true))).Should().ThrowAsync<ValidationException>();
        handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TestAndRotation_ExposeRateLimitWithoutRetry(bool rotate)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/webhooks/endpoints/ep", new { status = 429, message = "Rate limited" },
            System.Net.HttpStatusCode.TooManyRequests, new Dictionary<string, string> { ["Retry-After"] = "60" });
        var resource = CreateResource(handler);
        Func<Task> action = rotate
            ? () => resource.RotateEndpointSecretWithOptionsAsync("ep", true)
            : () => resource.TestEndpointAsync("ep");

        var error = (await action.Should().ThrowAsync<ApiException>()).Which;

        error.StatusCode.Should().Be(429);
        error.RetryAfter.Should().Be(TimeSpan.FromSeconds(60));
        handler.Requests.Should().ContainSingle();
    }
}
