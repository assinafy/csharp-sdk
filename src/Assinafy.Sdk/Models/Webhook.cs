using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>The account's oldest webhook endpoint, as exposed by the <c>/webhooks/subscriptions</c> routes.</summary>
public sealed record WebhookSubscription
{
    /// <summary>Event type codes subscribed for delivery (see <see cref="WebhookEventTypeInfo"/>).</summary>
    [JsonPropertyName("events")]
    public IReadOnlyList<string> Events { get; init; } = [];

    /// <summary>Whether webhook delivery is currently active.</summary>
    [JsonPropertyName("is_active")]
    public bool IsActive { get; init; }

    /// <summary>Webhook endpoint URL that receives events, or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>Contact email that receives important webhook-communication notices, or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    /// <summary>ISO-8601 date-time when the subscription was last updated, or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }
}

/// <summary>A webhook event type that can be subscribed to, as returned by <c>GET /webhooks/event-types</c>.</summary>
public sealed record WebhookEventTypeInfo
{
    /// <summary>Event type code (e.g. <c>document_ready</c>).</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable description of when the event is triggered.</summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}

/// <summary>A single webhook delivery-history entry.</summary>
public sealed record WebhookDispatch
{
    /// <summary>Resource type discriminator; <c>activity_dispatching_history</c> in single-resource responses.</summary>
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    /// <summary>The dispatch entry's unique identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Event type that triggered the dispatch.</summary>
    [JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    /// <summary>Internal activity ID associated with the dispatch.</summary>
    [JsonPropertyName("activity_id")]
    public long ActivityId { get; init; }

    /// <summary>URL that received the request, or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; init; }

    /// <summary>ID of the webhook endpoint the delivery was sent to, or <see langword="null"/> once that endpoint is deleted.</summary>
    [JsonPropertyName("endpoint_id")]
    public string? EndpointId { get; init; }

    /// <summary>JSON payload sent to the endpoint.</summary>
    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }

    /// <summary>Whether delivery succeeded.</summary>
    [JsonPropertyName("delivered")]
    public bool Delivered { get; init; }

    /// <summary>HTTP status code returned by the endpoint, or <see langword="null"/> when the connection failed.</summary>
    [JsonPropertyName("http_status")]
    public int? HttpStatus { get; init; }

    /// <summary>Endpoint response body, truncated to 2000 characters, or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("response_body")]
    public string? ResponseBody { get; init; }

    /// <summary>Delivery error message, if any.</summary>
    [JsonPropertyName("error")]
    public string? Error { get; init; }

    /// <summary>ISO-8601 date-time when the dispatch was created.</summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;

    /// <summary>ISO-8601 date-time when the dispatch was last updated, or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }
}

/// <summary>Body for <c>PUT /accounts/{account_id}/webhooks/subscriptions</c> — update the account's oldest webhook endpoint, creating it when the account has none.</summary>
public sealed class UpdateWebhookSubscriptionRequest
{
    /// <summary>Event type codes to subscribe to (see <c>GET /webhooks/event-types</c>).</summary>
    public required IReadOnlyList<string> Events { get; set; }

    /// <summary>Whether events should be delivered to the webhook.</summary>
    public required bool IsActive { get; set; }

    /// <summary>The URL that will receive events.</summary>
    public required string Url { get; set; }

    /// <summary>Email that receives important webhook-communication notices.</summary>
    public required string Email { get; set; }
}

/// <summary>Query parameters for listing webhook delivery history (<c>GET /accounts/{account_id}/webhooks</c>).</summary>
public sealed class ListDispatchesParams
{
    /// <summary>Page number to retrieve.</summary>
    public int? Page { get; set; }

    /// <summary>Items per page (default 20).</summary>
    public int? PerPage { get; set; }

    /// <summary>Only deliveries to this webhook endpoint (query <c>endpoint_id</c>).</summary>
    public string? EndpointId { get; set; }

    /// <summary>Filter by event type (e.g. <c>document_ready</c>).</summary>
    public string? Event { get; set; }

    /// <summary>Filter by delivery status — <see langword="true"/> for delivered, <see langword="false"/> for failed.</summary>
    public bool? Delivered { get; set; }

    /// <summary>Unix timestamp in seconds; include only entries after this time.</summary>
    public long? From { get; set; }

    /// <summary>Unix timestamp in seconds; include only entries before this time.</summary>
    public long? To { get; set; }
}

/// <summary>A URL that receives the account's webhook events. Every active endpoint subscribed to an event receives it.</summary>
public sealed record WebhookEndpoint
{
    /// <summary>Endpoint ID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Label to tell endpoints apart, or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>URL that receives the events (http or https).</summary>
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    /// <summary>Contact email for delivery-failure notices.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    /// <summary>Event type codes delivered to this endpoint (see <see cref="WebhookEventTypeInfo"/>).</summary>
    [JsonPropertyName("events")]
    public IReadOnlyList<string> Events { get; init; } = [];

    /// <summary>Whether events are delivered to this endpoint.</summary>
    [JsonPropertyName("is_active")]
    public bool IsActive { get; init; }

    /// <summary>Whether deliveries carry a <c>webhook-signature</c> header (see <see cref="Webhooks.WebhookSignature"/>).</summary>
    [JsonPropertyName("signing_enabled")]
    public bool SigningEnabled { get; init; }

    /// <summary>ISO-8601 date-time when the endpoint was created.</summary>
    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; init; }

    /// <summary>ISO-8601 date-time when the endpoint was last updated.</summary>
    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }
}

/// <summary>An endpoint's Standard Webhooks signing secret.</summary>
public sealed record WebhookEndpointSecret
{
    /// <summary><c>whsec_</c> followed by the base64-encoded key. Pass it to <see cref="Webhooks.WebhookSignature.Verify"/>.</summary>
    [JsonPropertyName("secret")]
    public string Secret { get; init; } = string.Empty;
}

/// <summary>Body for <c>POST /accounts/{account_id}/webhooks/endpoints</c>.</summary>
public sealed class CreateWebhookEndpointRequest
{
    /// <summary>URL that receives the events (http or https). Must differ from every other endpoint of the workspace.</summary>
    public required string Url { get; set; }

    /// <summary>Contact email for delivery-failure notices.</summary>
    public required string Email { get; set; }

    /// <summary>Event type codes to deliver (see <c>GET /webhooks/event-types</c>).</summary>
    public required IReadOnlyList<string> Events { get; set; }

    /// <summary>Label to tell endpoints apart, or <see langword="null"/> to omit.</summary>
    public string? Name { get; set; }

    /// <summary>Whether events are delivered; the API defaults to <see langword="true"/> when omitted.</summary>
    public bool? IsActive { get; set; }

    /// <summary>Sign deliveries with a Standard Webhooks signature; the API defaults to <see langword="false"/> when omitted.</summary>
    public bool? SigningEnabled { get; set; }
}

/// <summary>Body for <c>PUT /accounts/{account_id}/webhooks/endpoints/{endpoint_id}</c>. Only non-null properties are sent and updated.</summary>
public sealed class UpdateWebhookEndpointRequest
{
    /// <summary>New URL; must not be used by another endpoint of the workspace.</summary>
    public string? Url { get; set; }

    /// <summary>New contact email for delivery-failure notices.</summary>
    public string? Email { get; set; }

    /// <summary>New list of event type codes to deliver.</summary>
    public IReadOnlyList<string>? Events { get; set; }

    /// <summary>New label.</summary>
    public string? Name { get; set; }

    /// <summary>Whether events are delivered.</summary>
    public bool? IsActive { get; set; }

    /// <summary><see langword="true"/> generates a secret when the endpoint has none and keeps the current one otherwise; <see langword="false"/> discards the secret.</summary>
    public bool? SigningEnabled { get; set; }
}

/// <summary>
/// The JSON body Assinafy POSTs to a webhook endpoint. Deserialize the raw body with
/// <see cref="JsonSerializer"/> after checking it with <see cref="Webhooks.WebhookSignature.Verify"/>.
/// </summary>
public sealed record WebhookEvent
{
    /// <summary>Internal activity ID that produced the event. Deduplicate with the <c>webhook-id</c> header instead, which also tells deliveries of one event to different endpoints apart.</summary>
    [JsonPropertyName("id")]
    public long Id { get; init; }

    /// <summary>Event type code (e.g. <c>document_ready</c>).</summary>
    [JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    /// <summary>Human-readable message, which may contain token placeholders, or <see langword="null"/>.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>Event-specific parameters (keys vary per event), or <see langword="null"/>.</summary>
    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }

    /// <summary>Where the action was triggered from (<c>{ ip, user-agent }</c>), or <see langword="null"/>.</summary>
    [JsonPropertyName("origin")]
    public JsonElement? Origin { get; init; }

    /// <summary>Unix timestamp (seconds) when the event was recorded.</summary>
    [JsonPropertyName("created_at")]
    public long CreatedAt { get; init; }

    /// <summary>The entity that performed the action; its <c>type</c> property is <c>User</c>, <c>Signer</c>, <c>Account</c>, <c>Document</c>, or <c>Template</c>.</summary>
    [JsonPropertyName("subject")]
    public JsonElement Subject { get; init; }

    /// <summary>The entity the action was performed on, with its relationships expanded; discriminated by its <c>type</c> property.</summary>
    [JsonPropertyName("object")]
    public JsonElement Object { get; init; }

    /// <summary>ID of the account that owns the event.</summary>
    [JsonPropertyName("account_id")]
    public string AccountId { get; init; } = string.Empty;
}
