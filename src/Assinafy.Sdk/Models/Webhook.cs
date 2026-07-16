using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>An account's webhook subscription configuration.</summary>
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

/// <summary>Body for <c>PUT /accounts/{account_id}/webhooks/subscriptions</c> — update the account's webhook subscription.</summary>
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

    /// <summary>Filter by event type (e.g. <c>document_ready</c>).</summary>
    public string? Event { get; set; }

    /// <summary>Filter by delivery status — <see langword="true"/> for delivered, <see langword="false"/> for failed.</summary>
    public bool? Delivered { get; set; }

    /// <summary>Unix timestamp in seconds; include only entries after this time.</summary>
    public long? From { get; set; }

    /// <summary>Unix timestamp in seconds; include only entries before this time.</summary>
    public long? To { get; set; }
}
