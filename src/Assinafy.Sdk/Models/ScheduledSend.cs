using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>An assignment or envelope request retained until its scheduled send time. Available on staging.</summary>
public sealed record ScheduledSend
{
    /// <summary>Resource discriminator: scheduled_send.</summary>
    [JsonPropertyName("resource")]
    public string Resource { get; init; } = string.Empty;

    /// <summary>Scheduled send identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Request type: assignment or envelope.</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>Documents to send; deleted documents are omitted.</summary>
    [JsonPropertyName("document_ids")]
    public IReadOnlyList<string> DocumentIds { get; init; } = [];

    /// <summary>pending, processing, sent, cancelled or failed. Sent means created, with invitations queued.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>Scheduled time as an ISO-8601 timestamp.</summary>
    [JsonPropertyName("send_at")]
    public string SendAt { get; init; } = string.Empty;

    /// <summary>Created assignment ID once an assignment send succeeds.</summary>
    [JsonPropertyName("assignment_id")]
    public string? AssignmentId { get; init; }

    /// <summary>Created envelope ID once an envelope send succeeds; null again after envelope deletion.</summary>
    [JsonPropertyName("envelope_id")]
    public string? EnvelopeId { get; init; }

    /// <summary>Failure explanation in Portuguese, or null. A failed send charges nothing.</summary>
    [JsonPropertyName("failure_reason")]
    public string? FailureReason { get; init; }

    /// <summary>Creation time as an ISO-8601 timestamp.</summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;

    /// <summary>Time sent, failed or cancelled; null while pending.</summary>
    [JsonPropertyName("processed_at")]
    public string? ProcessedAt { get; init; }
}
