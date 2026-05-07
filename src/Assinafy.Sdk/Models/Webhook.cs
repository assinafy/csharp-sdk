using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

public sealed record WebhookSubscription
{
    [JsonPropertyName("events")]
    public IReadOnlyList<string> Events { get; init; } = [];

    [JsonPropertyName("is_active")]
    public bool IsActive { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }
}

public sealed record WebhookEventTypeInfo
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}

public sealed record WebhookDispatch
{
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    [JsonPropertyName("activity_id")]
    public long ActivityId { get; init; }

    [JsonPropertyName("endpoint")]
    public string? Endpoint { get; init; }

    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }

    [JsonPropertyName("delivered")]
    public bool Delivered { get; init; }

    [JsonPropertyName("http_status")]
    public int? HttpStatus { get; init; }

    [JsonPropertyName("response_body")]
    public string? ResponseBody { get; init; }

    [JsonPropertyName("error")]
    public string? Error { get; init; }

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }
}

public sealed class UpdateWebhookSubscriptionRequest
{
    public required IReadOnlyList<string> Events { get; set; }
    public required bool IsActive { get; set; }
    public required string Url { get; set; }
    public required string Email { get; set; }
}

public sealed class ListDispatchesParams
{
    public int? Page { get; set; }
    public int? PerPage { get; set; }
    public string? Event { get; set; }
    public bool? Delivered { get; set; }
    public long? From { get; set; }
    public long? To { get; set; }
}
