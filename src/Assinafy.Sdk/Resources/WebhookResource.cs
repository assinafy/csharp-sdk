using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>
/// Webhook endpoints, signing secrets, event types, and delivery history. An account can register one
/// endpoint, or up to three on paid plans; the <c>/webhooks/subscriptions</c> methods act on the oldest.
/// </summary>
public sealed class WebhookResource : BaseResource
{
    internal WebhookResource(HttpClient http, string? defaultAccountId = null, Action<HttpRequestMessage>? authenticate = null)
        : base(http, defaultAccountId, authenticate) { }

    /// <summary><c>PUT /accounts/{account_id}/webhooks/subscriptions</c> — update the account's oldest webhook endpoint, creating it when the account has none. Accounts with several endpoints use <see cref="UpdateEndpointAsync"/>.</summary>
    /// <param name="request">Subscription settings: at least one event, plus the delivery URL and contact email.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created or updated webhook subscription.</returns>
    public Task<WebhookSubscription> UpdateSubscriptionAsync(
        UpdateWebhookSubscriptionRequest request,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Events is null || request.Events.Count == 0)
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

    /// <summary><c>GET /accounts/{account_id}/webhooks/subscriptions</c> — fetch the account's oldest webhook endpoint. Accounts with several endpoints use <see cref="ListEndpointsAsync"/>.</summary>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The account's oldest webhook endpoint configuration.</returns>
    public Task<WebhookSubscription> GetAsync(
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        return CallAsync<WebhookSubscription>(
            $"accounts/{id}/webhooks/subscriptions",
            HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>PUT /accounts/{account_id}/webhooks/inactivate</c> — pause delivery to the account's oldest endpoint without losing its configuration. Other endpoints are unaffected; use <see cref="UpdateEndpointAsync"/> or <see cref="DeleteEndpointAsync"/> for them.</summary>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The inactive webhook subscription.</returns>
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
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The webhook event types supported by the platform.</returns>
    public async Task<IReadOnlyList<WebhookEventTypeInfo>> ListEventTypesAsync(
        CancellationToken cancellationToken = default)
    {
        return await CallListBodyAsync<WebhookEventTypeInfo>(
            "webhooks/event-types",
            HttpMethod.Get,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary><c>GET /accounts/{account_id}/webhooks</c> — list delivery history across the account's endpoints with optional filters (<c>endpoint_id</c>, <c>event</c>, <c>delivered</c>, <c>from</c>, <c>to</c>, <c>page</c>, <c>per-page</c>).</summary>
    /// <param name="parameters">Optional filters: endpoint, event type, delivered flag, unix-epoch <c>from</c>/<c>to</c> bounds (seconds), and pagination.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated collection of webhook delivery attempts.</returns>
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

    /// <summary><c>POST /accounts/{account_id}/webhooks/{dispatch_id}/retry</c> — re-send a previous delivery to the endpoint of that entry only. Returns 400 when that endpoint is inactive or no longer subscribed to the event.</summary>
    /// <param name="dispatchId">Previous webhook dispatch to re-attempt.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The webhook dispatch record after the retry request.</returns>
    public Task<WebhookDispatch> RetryDispatchAsync(
        string dispatchId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var dispatch = PathSegment(dispatchId, "Dispatch ID");
        return CallAsync<WebhookDispatch>(
            $"accounts/{id}/webhooks/{dispatch}/retry",
            HttpMethod.Post,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /accounts/{account_id}/webhooks/endpoints</c> — list the account's webhook endpoints, oldest first. OAuth scope <c>account:read</c>.</summary>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The account's webhook endpoints.</returns>
    public Task<IReadOnlyList<WebhookEndpoint>> ListEndpointsAsync(
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        return CallListBodyAsync<WebhookEndpoint>(
            $"accounts/{id}/webhooks/endpoints",
            HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /accounts/{account_id}/webhooks/endpoints/{endpoint_id}</c> — fetch one webhook endpoint. OAuth scope <c>account:read</c>.</summary>
    /// <param name="endpointId">Endpoint to fetch.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The webhook endpoint.</returns>
    public Task<WebhookEndpoint> GetEndpointAsync(
        string endpointId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        return CallAsync<WebhookEndpoint>(
            EndpointPath(endpointId, accountId),
            HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// <c>POST /accounts/{account_id}/webhooks/endpoints</c> — register a URL to receive the account's events.
    /// Returns 403 past the plan's limit (one endpoint, or three on paid plans) and 400 when another endpoint
    /// already uses the URL. With <see cref="CreateWebhookEndpointRequest.SigningEnabled"/> a secret is
    /// generated; read it with <see cref="GetEndpointSecretAsync"/>. OAuth scope <c>webhooks:write</c>.
    /// </summary>
    /// <param name="request">URL, contact email, events, and optional name, active flag, and signing flag.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created webhook endpoint.</returns>
    public Task<WebhookEndpoint> CreateEndpointAsync(
        CreateWebhookEndpointRequest request,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Url);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        if (request.Events is null || request.Events.Count == 0)
            throw new ValidationException("At least one webhook event is required.");

        var id = AccountId(accountId);
        return CallAsync<WebhookEndpoint>(
            $"accounts/{id}/webhooks/endpoints",
            HttpMethod.Post,
            request,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// <c>PUT /accounts/{account_id}/webhooks/endpoints/{endpoint_id}</c> — change only the fields sent.
    /// Enabling signing generates a secret when the endpoint has none; disabling it discards the secret.
    /// Returns 400 when another endpoint already uses the URL. OAuth scope <c>webhooks:write</c>.
    /// </summary>
    /// <param name="endpointId">Endpoint to update.</param>
    /// <param name="request">The fields to change; null properties are omitted.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated webhook endpoint.</returns>
    public Task<WebhookEndpoint> UpdateEndpointAsync(
        string endpointId,
        UpdateWebhookEndpointRequest request,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Events is { Count: 0 })
            throw new ValidationException("At least one webhook event is required when events are sent.");

        return CallAsync<WebhookEndpoint>(
            EndpointPath(endpointId, accountId),
            HttpMethod.Put,
            request,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>DELETE /accounts/{account_id}/webhooks/endpoints/{endpoint_id}</c> — stop delivering to an endpoint and free its slot. OAuth scope <c>webhooks:write</c>.</summary>
    /// <param name="endpointId">Endpoint to delete.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the endpoint has been deleted.</returns>
    public Task DeleteEndpointAsync(
        string endpointId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        return CallVoidAsync(
            EndpointPath(endpointId, accountId),
            HttpMethod.Delete,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// <c>GET /accounts/{account_id}/webhooks/endpoints/{endpoint_id}/secret</c> — read the secret that signs
    /// deliveries to the endpoint. Returns 400 when signing is disabled. Not available to OAuth applications.
    /// </summary>
    /// <param name="endpointId">Endpoint whose secret to read.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <c>whsec_</c> signing secret.</returns>
    public Task<WebhookEndpointSecret> GetEndpointSecretAsync(
        string endpointId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        return CallAsync<WebhookEndpointSecret>(
            $"{EndpointPath(endpointId, accountId)}/secret",
            HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// <c>POST /accounts/{account_id}/webhooks/endpoints/{endpoint_id}/secret/rotate</c> — replace the signing
    /// secret. The old secret stops working immediately, so update the receiver at once. Returns 400 when
    /// signing is disabled. Not available to OAuth applications.
    /// </summary>
    /// <param name="endpointId">Endpoint whose secret to rotate.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new <c>whsec_</c> signing secret.</returns>
    public Task<WebhookEndpointSecret> RotateEndpointSecretAsync(
        string endpointId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        return CallAsync<WebhookEndpointSecret>(
            $"{EndpointPath(endpointId, accountId)}/secret/rotate",
            HttpMethod.Post,
            cancellationToken: cancellationToken);
    }

    private string EndpointPath(string endpointId, string? accountId) =>
        $"accounts/{AccountId(accountId)}/webhooks/endpoints/{PathSegment(endpointId, "Endpoint ID")}";

    private static IDictionary<string, string?>? BuildDispatchQueryParams(ListDispatchesParams? parameters)
    {
        if (parameters is null) return null;

        var query = new Dictionary<string, string?>();
        if (parameters.Page.HasValue) query["page"] = parameters.Page.Value.ToString();
        if (parameters.PerPage.HasValue) query["per-page"] = parameters.PerPage.Value.ToString();
        if (!string.IsNullOrWhiteSpace(parameters.EndpointId)) query["endpoint_id"] = parameters.EndpointId;
        if (!string.IsNullOrWhiteSpace(parameters.Event)) query["event"] = parameters.Event;
        if (parameters.Delivered.HasValue) query["delivered"] = parameters.Delivered.Value ? "true" : "false";
        if (parameters.From.HasValue) query["from"] = parameters.From.Value.ToString();
        if (parameters.To.HasValue) query["to"] = parameters.To.Value.ToString();

        return query.Count > 0 ? query : null;
    }
}
