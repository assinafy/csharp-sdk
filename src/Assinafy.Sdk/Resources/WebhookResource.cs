using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Webhook subscriptions, event types, and delivery history.</summary>
public sealed class WebhookResource : BaseResource
{
    internal WebhookResource(HttpClient http, string? defaultAccountId = null)
        : base(http, defaultAccountId) { }

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

    public Task DeleteAsync(string? accountId = null, CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        return CallVoidAsync(
            $"accounts/{id}/webhooks/subscriptions",
            HttpMethod.Delete,
            cancellationToken: cancellationToken);
    }

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

    public async Task<IReadOnlyList<WebhookEventTypeInfo>> ListEventTypesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await CallAsync<List<WebhookEventTypeInfo>>(
            "webhooks/event-types",
            HttpMethod.Get,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result ?? [];
    }

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
