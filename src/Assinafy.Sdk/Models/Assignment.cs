using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

public sealed record AssignmentSigner : Signer
{
    [JsonPropertyName("verification_method")]
    public string? VerificationMethod { get; init; }

    [JsonPropertyName("notification_methods")]
    public IReadOnlyList<string>? NotificationMethods { get; init; }

    [JsonPropertyName("completed")]
    public bool? Completed { get; init; }

    /// <summary>Signing order step. Signers in the same step sign in parallel; the next step activates once the previous completes.</summary>
    [JsonPropertyName("step")]
    public int? Step { get; init; }

    [JsonPropertyName("notified")]
    public bool? Notified { get; init; }

    [JsonPropertyName("notification_history")]
    public JsonElement? NotificationHistory { get; init; }
}

public sealed record AssignmentSummary
{
    [JsonPropertyName("signer_count")]
    public int SignerCount { get; init; }

    [JsonPropertyName("completed_count")]
    public int CompletedCount { get; init; }

    [JsonPropertyName("signers")]
    public IReadOnlyList<AssignmentSigner> Signers { get; init; } = [];
}

public sealed record AssignmentItem
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("page")]
    public DocumentPage? Page { get; init; }

    [JsonPropertyName("signer")]
    public AssignmentSigner? Signer { get; init; }

    [JsonPropertyName("field")]
    public FieldDefinition? Field { get; init; }

    [JsonPropertyName("display_settings")]
    public JsonElement? DisplaySettings { get; init; }

    [JsonPropertyName("value")]
    public JsonElement? Value { get; init; }

    [JsonPropertyName("completed")]
    public bool Completed { get; init; }
}

public sealed record SigningUrl
{
    [JsonPropertyName("signer_id")]
    public string SignerId { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;
}

public sealed record Assignment
{
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("sender_email")]
    public string? SenderEmail { get; init; }

    [JsonPropertyName("method")]
    public string? Method { get; init; }

    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; init; }

    [JsonPropertyName("expiration")]
    public string? Expiration { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("signers")]
    public IReadOnlyList<AssignmentSigner> Signers { get; init; } = [];

    [JsonPropertyName("copy_receivers")]
    public IReadOnlyList<Signer> CopyReceivers { get; init; } = [];

    [JsonPropertyName("items")]
    public IReadOnlyList<AssignmentItem> Items { get; init; } = [];

    [JsonPropertyName("summary")]
    public AssignmentSummary? Summary { get; init; }

    [JsonPropertyName("signing_urls")]
    public IReadOnlyList<SigningUrl> SigningUrls { get; init; } = [];
}

public sealed class SignerRef
{
    public string? Id { get; set; }
    public string? VerificationMethod { get; set; }
    public string[]? NotificationMethods { get; set; }

    public static implicit operator SignerRef(string id) => new() { Id = id };
}

public sealed class CreateAssignmentRequest
{
    public string? Method { get; set; }
    public List<SignerRef>? Signers { get; set; }
    public string[]? SignerIds { get; set; }
    public string? Message { get; set; }
    public string? ExpiresAt { get; set; }
    public string[]? CopyReceivers { get; set; }
    public IReadOnlyList<AssignmentEntry>? Entries { get; set; }
}

public sealed class AssignmentEntry
{
    public required string PageId { get; set; }
    public required IReadOnlyList<AssignmentEntryField> Fields { get; set; }
}

public sealed class AssignmentEntryField
{
    public required string SignerId { get; set; }
    public required string FieldId { get; set; }
    public JsonElement? DisplaySettings { get; set; }
}

public sealed record AssignmentCostEstimate
{
    [JsonPropertyName("documents")]
    public decimal Documents { get; init; }

    [JsonPropertyName("credits")]
    public decimal Credits { get; init; }

    [JsonPropertyName("needs_extra_document")]
    public bool NeedsExtraDocument { get; init; }

    [JsonPropertyName("extra_document_cost")]
    public decimal ExtraDocumentCost { get; init; }

    [JsonPropertyName("total_credits")]
    public decimal TotalCredits { get; init; }

    [JsonPropertyName("breakdown")]
    public IReadOnlyList<CostBreakdownItem> Breakdown { get; init; } = [];

    [JsonPropertyName("document_balance")]
    public decimal DocumentBalance { get; init; }

    [JsonPropertyName("credit_balance")]
    public decimal CreditBalance { get; init; }

    [JsonPropertyName("has_sufficient_resources")]
    public bool HasSufficientResources { get; init; }

    /// <summary>Reason resources are insufficient (e.g. <c>PendingPayment</c>, <c>InsufficientDocuments</c>, <c>InsufficientCredits</c>), or <see langword="null"/> when the request can proceed.</summary>
    [JsonPropertyName("blocking_reason")]
    public string? BlockingReason { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

public sealed record ResendNotificationResult
{
    [JsonPropertyName("is_sent")]
    public bool IsSent { get; init; }

    [JsonPropertyName("document_id")]
    public string DocumentId { get; init; } = string.Empty;

    [JsonPropertyName("signer_id")]
    public string SignerId { get; init; } = string.Empty;
}

public sealed record ResendCostEstimate
{
    [JsonPropertyName("total")]
    public decimal Total { get; init; }

    [JsonPropertyName("breakdown")]
    public IReadOnlyList<CostBreakdownItem> Breakdown { get; init; } = [];

    [JsonPropertyName("credit_balance")]
    public decimal CreditBalance { get; init; }

    [JsonPropertyName("has_sufficient_credits")]
    public bool HasSufficientCredits { get; init; }
}

public sealed record CostBreakdownItem
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("cost")]
    public decimal Cost { get; init; }

    [JsonPropertyName("quantity")]
    public decimal? Quantity { get; init; }

    [JsonPropertyName("unit_cost")]
    public decimal? UnitCost { get; init; }
}

public sealed record WhatsAppNotification
{
    [JsonPropertyName("sent_at")]
    public long SentAt { get; init; }

    [JsonPropertyName("header")]
    public string Header { get; init; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;

    [JsonPropertyName("buttons")]
    public IReadOnlyList<WhatsAppNotificationButton> Buttons { get; init; } = [];

    [JsonPropertyName("phone_number")]
    public string PhoneNumber { get; init; } = string.Empty;

    [JsonPropertyName("signer_id")]
    public string SignerId { get; init; } = string.Empty;
}

public sealed record WhatsAppNotificationButton
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; init; }
}

public sealed class TemplateSigner
{
    public required string RoleId { get; set; }
    public required string Id { get; set; }
    public string? VerificationMethod { get; set; }
    public string[]? NotificationMethods { get; set; }
}
