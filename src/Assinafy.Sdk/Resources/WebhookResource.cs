using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Webhook subscriptions, event types, and delivery history.</summary>
public sealed class WebhookResource : BaseResource
{
    internal WebhookResource(HttpClient http, string? defaultAccountId = null)
        : base(http, defaultAccountId) { }

    /// <summary><c>PUT /accounts/{account_id}/webhooks/subscriptions</c> — create or replace the workspace's webhook subscription.</summary>
    public Task<WebhookSubscription> UpdateSubscriptionAsync(
        UpdateWebhookSubscriptionRequest request,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Events.Count == 0)
            throw new ValidationException("At least one webhook event is required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Url);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);

        var id = AccountId(accountId);
        return CallAsync<WebhookSubscription>(
            $"accounts/{id}/webhooks/subscriptions",
            HttpMethod.Put,
            request,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /accounts/{account_id}/webhooks/subscriptions</c> — fetch the workspace's current webhook subscription. Returns <see langword="null"/> if there is no subscription.</summary>
    public async Task<WebhookSubscription?> GetAsync(
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        try
        {
            return await CallAsync<WebhookSubscription>(
                $"accounts/{id}/webhooks/subscriptions",
                HttpMethod.Get,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (ApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    /// <summary><c>DELETE /accounts/{account_id}/webhooks/subscriptions</c> — remove the workspace's webhook subscription entirely.</summary>
    public Task DeleteAsync(string? accountId = null, CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        return CallVoidAsync(
            $"accounts/{id}/webhooks/subscriptions",
            HttpMethod.Delete,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>PUT /accounts/{account_id}/webhooks/inactivate</c> — pause delivery without losing the subscription configuration.</summary>
    public Task<WebhookSubscription> InactivateAsync(
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        return CallAsync<WebhookSubscription>(
            $"accounts/{id}/webhooks/inactivate",
            HttpMethod.Put,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /webhooks/event-types</c> — list all event types supported by the platform.</summary>
    public async Task<IReadOnlyList<WebhookEventTypeInfo>> ListEventTypesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await CallAsync<List<WebhookEventTypeInfo>>(
            "webhooks/event-types",
            HttpMethod.Get,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result ?? [];
    }

    /// <summary><c>GET /accounts/{account_id}/webhooks</c> — list webhook delivery history with optional filters (<c>event</c>, <c>delivered</c>, <c>from</c>, <c>to</c>, <c>page</c>, <c>per-page</c>).</summary>
    public Task<PaginatedResult<WebhookDispatch>> ListDispatchesAsync(
        ListDispatchesParams? parameters = null,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        return CallListAsync<WebhookDispatch>(
            $"accounts/{id}/webhooks",
            BuildDispatchQueryParams(parameters),
            cancellationToken);
    }

    /// <summary><c>POST /accounts/{account_id}/webhooks/{dispatch_id}/retry</c> — re-attempt delivery of a previous webhook dispatch.</summary>
    public Task<WebhookDispatch> RetryDispatchAsync(
        string dispatchId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var dispatch = RequireId(dispatchId, "Dispatch ID");
        return CallAsync<WebhookDispatch>(
            $"accounts/{id}/webhooks/{dispatch}/retry",
            HttpMethod.Post,
            cancellationToken: cancellationToken);
    }

    private static IDictionary<string, string?>? BuildDispatchQueryParams(ListDispatchesParams? parameters)
    {
        if (parameters is null) return null;

        var query = new Dictionary<string, string?>();
        if (parameters.Page.HasValue) query["page"] = parameters.Page.Value.ToString();
        if (parameters.PerPage.HasValue) query["per-page"] = parameters.PerPage.Value.ToString();
        if (!string.IsNullOrWhiteSpace(parameters.Event)) query["event"] = parameters.Event;
        if (parameters.Delivered.HasValue) query["delivered"] = parameters.Delivered.Value ? "true" : "false";
        if (parameters.From.HasValue) query["from"] = parameters.From.Value.ToString();
        if (parameters.To.HasValue) query["to"] = parameters.To.Value.ToString();

        return query.Count > 0 ? query : null;
    }
}
