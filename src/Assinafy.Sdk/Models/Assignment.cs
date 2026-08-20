using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>A signer within an assignment: the base <see cref="Signer"/> plus per-assignment verification, notification, and progress details.</summary>
public sealed record AssignmentSigner : Signer
{
    /// <summary>How the signer's identity is verified before signing — one of <see cref="SignerChannels"/> (<c>Email</c> by default, or <c>Whatsapp</c>), or <see langword="null"/>.</summary>
    [JsonPropertyName("verification_method")]
    public string? VerificationMethod { get; init; }

    /// <summary>Channels used to notify the signer of the request — any combination of <see cref="SignerChannels"/> (<c>Email</c>, <c>Whatsapp</c>), or <see langword="null"/>.</summary>
    [JsonPropertyName("notification_methods")]
    public IReadOnlyList<string>? NotificationMethods { get; init; }

    /// <summary>Whether this signer has completed signing. Only present in account-owner contexts.</summary>
    [JsonPropertyName("completed")]
    public bool? Completed { get; init; }

    /// <summary>Signing order step. Signers in the same step sign in parallel; the next step activates once the previous completes.</summary>
    [JsonPropertyName("step")]
    public int? Step { get; init; }

    /// <summary>Whether the signer has been notified of the signature request.</summary>
    [JsonPropertyName("notified")]
    public bool? Notified { get; init; }

    /// <summary>Per-channel delivery history for this signer (email + WhatsApp), most recent first. Present in account-owner contexts.</summary>
    [JsonPropertyName("notification_history")]
    public IReadOnlyList<NotificationHistoryEntry>? NotificationHistory { get; init; }
}

/// <summary>One delivery attempt of a signer notification (email or WhatsApp), returned in <see cref="AssignmentSigner.NotificationHistory"/>.</summary>
public sealed record NotificationHistoryEntry
{
    /// <summary>The notification event/channel (e.g. the notification type dispatched).</summary>
    [JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    /// <summary>Delivery status: <c>sent</c> or <c>failed</c>.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>Provider error code when <see cref="Status"/> is <c>failed</c>, otherwise <see langword="null"/>.</summary>
    [JsonPropertyName("error_code")]
    public string? ErrorCode { get; init; }

    /// <summary>Human-readable error detail when <see cref="Status"/> is <c>failed</c>, otherwise <see langword="null"/>.</summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }

    /// <summary>ISO-8601 timestamp of successful delivery, or <see langword="null"/>.</summary>
    [JsonPropertyName("sent_at")]
    public string? SentAt { get; init; }

    /// <summary>ISO-8601 timestamp of the failed attempt, or <see langword="null"/>.</summary>
    [JsonPropertyName("failed_at")]
    public string? FailedAt { get; init; }
}

/// <summary>Aggregate signing progress for an assignment.</summary>
public sealed record AssignmentSummary
{
    /// <summary>Total number of signers on the assignment.</summary>
    [JsonPropertyName("signer_count")]
    public int SignerCount { get; init; }

    /// <summary>Number of signers who have completed signing.</summary>
    [JsonPropertyName("completed_count")]
    public int CompletedCount { get; init; }

    /// <summary>The assignment's signers.</summary>
    [JsonPropertyName("signers")]
    public IReadOnlyList<AssignmentSigner> Signers { get; init; } = [];
}

/// <summary>A single field placement within an assignment (the <c>collect</c> method): the page, signer, field, and captured value.</summary>
public sealed record AssignmentItem
{
    /// <summary>The assignment item ID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>The document page this item is placed on.</summary>
    [JsonPropertyName("page")]
    public DocumentPage? Page { get; init; }

    /// <summary>The signer responsible for this item.</summary>
    [JsonPropertyName("signer")]
    public AssignmentSigner? Signer { get; init; }

    /// <summary>The field definition associated with this item.</summary>
    [JsonPropertyName("field")]
    public FieldDefinition? Field { get; init; }

    /// <summary>Rendering metadata (position, appearance) for the item.</summary>
    [JsonPropertyName("display_settings")]
    public JsonElement? DisplaySettings { get; init; }

    /// <summary>The captured value, present once the item is completed.</summary>
    [JsonPropertyName("value")]
    public JsonElement? Value { get; init; }

    /// <summary>Whether this item has been filled in / signed.</summary>
    [JsonPropertyName("completed")]
    public bool Completed { get; init; }
}

/// <summary>A signer's direct signing link for an assignment.</summary>
public sealed record SigningUrl
{
    /// <summary>The signer this URL belongs to.</summary>
    [JsonPropertyName("signer_id")]
    public string SignerId { get; init; } = string.Empty;

    /// <summary>The direct link the signer uses to open and sign the document.</summary>
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;
}

/// <summary>A request for signers to sign a document, with its signers, collected items, progress summary, and signing links.</summary>
public sealed record Assignment
{
    /// <summary>Resource type discriminator (<c>assignment</c>). Present in single-resource responses.</summary>
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    /// <summary>The assignment ID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Email address of the account owner who created the assignment.</summary>
    [JsonPropertyName("sender_email")]
    public string? SenderEmail { get; init; }

    /// <summary>Assignment method — one of <see cref="AssignmentMethods"/> (<c>virtual</c> or <c>collect</c>).</summary>
    [JsonPropertyName("method")]
    public string? Method { get; init; }

    /// <summary>Expiration as an ISO-8601 date-time string, or <see langword="null"/> when the assignment never expires.</summary>
    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; init; }

    /// <summary>Message included in the invitation sent to signers, or <see langword="null"/>.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>The signers requested to sign the document.</summary>
    [JsonPropertyName("signers")]
    public IReadOnlyList<AssignmentSigner> Signers { get; init; } = [];

    /// <summary>Signers who only receive a copy of the document; they are not asked to sign.</summary>
    [JsonPropertyName("copy_receivers")]
    public IReadOnlyList<Signer> CopyReceivers { get; init; } = [];

    /// <summary>Field placements collected on the document (the <c>collect</c> method).</summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<AssignmentItem> Items { get; init; } = [];

    /// <summary>Aggregate signing progress for the assignment.</summary>
    [JsonPropertyName("summary")]
    public AssignmentSummary? Summary { get; init; }

    /// <summary>Direct signing links, one per signer.</summary>
    [JsonPropertyName("signing_urls")]
    public IReadOnlyList<SigningUrl> SigningUrls { get; init; } = [];
}

/// <summary>Allowed values for an assignment's <c>method</c> (see <see cref="CreateAssignmentRequest.Method"/>).</summary>
public static class AssignmentMethods
{
    /// <summary>Signers are notified and sign remotely at their own convenience (the common flow).</summary>
    public const string Virtual = "virtual";

    /// <summary>Field values are collected in-session against explicit page/field placements (requires <see cref="CreateAssignmentRequest.Entries"/>).</summary>
    public const string Collect = "collect";
}

/// <summary>
/// Allowed verification and notification channel values. <c>Email</c> and <c>Whatsapp</c> may be
/// used for either purpose; <c>DigitalCertificate</c> is a verification method only. The API expects
/// these values capitalized. <c>Whatsapp</c> incurs an additional cost and is paid-only.
/// </summary>
public static class SignerChannels
{
    /// <summary>Email channel (the default when unset).</summary>
    public const string Email = "Email";

    /// <summary>WhatsApp channel (paid subscriptions only; additional cost).</summary>
    public const string Whatsapp = "Whatsapp";

    /// <summary>ICP-Brasil digital-certificate verification.</summary>
    public const string DigitalCertificate = "DigitalCertificate";
}

/// <summary>
/// Reference to an existing signer to include in a <see cref="CreateAssignmentRequest"/>, with
/// optional per-signer verification, notification, and ordering settings. A bare signer-ID string
/// converts implicitly to a <see cref="SignerRef"/>.
/// </summary>
public sealed class SignerRef
{
    /// <summary>The ID of an existing signer in the account.</summary>
    public string? Id { get; set; }

    /// <summary>How the signer's identity is verified before signing — <c>Email</c>, <c>Whatsapp</c>, or <c>DigitalCertificate</c>. If a notification-capable channel is set without <see cref="NotificationMethods"/>, it is also used for notification.</summary>
    public string? VerificationMethod { get; set; }

    /// <summary>The single channel used to notify the signer: <c>Email</c> or <c>Whatsapp</c>. If set without <see cref="VerificationMethod"/>, it also determines the verification channel.</summary>
    public string[]? NotificationMethods { get; set; }

    /// <summary>Signing-order step. Signers sharing a step sign in parallel; the next step activates once the previous step completes. Omit for all-at-once signing.</summary>
    public int? Step { get; set; }

    /// <summary>Creates a <see cref="SignerRef"/> from a bare signer ID, applying default verification/notification settings.</summary>
    public static implicit operator SignerRef(string id) => new() { Id = id };
}

/// <summary>Optional pagination filters for <c>Assignments.ListAsync</c> (<c>GET /assignments</c>).</summary>
public sealed class AssignmentListParams
{
    /// <summary>1-based page number.</summary>
    public int? Page { get; set; }

    /// <summary>Items per page (API default is 20).</summary>
    public int? PerPage { get; set; }
}

/// <summary>Body for <c>POST /v1/documents/{documentId}/assignments</c> (create assignment / request signatures).</summary>
public sealed class CreateAssignmentRequest
{
    /// <summary>Assignment method — one of <see cref="AssignmentMethods"/> (<c>virtual</c> to sign without input fields, or <c>collect</c> to place input fields on specific pages).</summary>
    public string? Method { get; set; }

    /// <summary>Signers to request, each with optional per-signer verification/notification/step settings. Required for the <c>virtual</c> method. Takes precedence over <see cref="SignerIds"/>.</summary>
    public List<SignerRef>? Signers { get; set; }

    /// <summary>Convenience shortcut for supplying signer IDs with default settings, used only when <see cref="Signers"/> is empty.</summary>
    public string[]? SignerIds { get; set; }

    /// <summary>Text included in the invitation sent to signers.</summary>
    public string? Message { get; set; }

    /// <summary>Expiration as an ISO-8601 date-time string. Defaults to no expiration.</summary>
    public string? ExpiresAt { get; set; }

    /// <summary>Signer IDs that only receive a copy of the document.</summary>
    public string[]? CopyReceivers { get; set; }

    /// <summary>Field placements per page. Required for the <c>collect</c> method.</summary>
    public IReadOnlyList<AssignmentEntry>? Entries { get; set; }
}

/// <summary>A page's field placements for a <c>collect</c> assignment (see <see cref="CreateAssignmentRequest.Entries"/>).</summary>
public sealed class AssignmentEntry
{
    /// <summary>The ID of the page the fields are placed on.</summary>
    public required string PageId { get; set; }

    /// <summary>The field placements on this page.</summary>
    public required IReadOnlyList<AssignmentEntryField> Fields { get; set; }
}

/// <summary>
/// Placement rectangle for a collect field. Coordinates use the document page's 150-DPI image
/// coordinate system and are measured from its upper-left corner.
/// </summary>
public sealed class DisplaySettings
{
    /// <summary>Horizontal distance from the page's left edge, in pixels.</summary>
    [JsonPropertyName("left")]
    public required double Left { get; set; }

    /// <summary>Vertical distance from the page's top edge, in pixels.</summary>
    [JsonPropertyName("top")]
    public required double Top { get; set; }

    /// <summary>Positive rectangle width, in pixels.</summary>
    [JsonPropertyName("width")]
    public required double Width { get; set; }

    /// <summary>Positive rectangle height, in pixels.</summary>
    [JsonPropertyName("height")]
    public required double Height { get; set; }

    /// <summary>Positive font size in the page-image coordinate system.</summary>
    [JsonPropertyName("fontSize")]
    public required double FontSize { get; set; }

    /// <summary>Optional font-family presentation metadata.</summary>
    [JsonPropertyName("fontFamily")]
    public string? FontFamily { get; set; }

    /// <summary>Optional CSS-compatible background color.</summary>
    [JsonPropertyName("backgroundColor")]
    public string? BackgroundColor { get; set; }
}

/// <summary>A single field placement on a page, tying a signer to a field.</summary>
public sealed class AssignmentEntryField
{
    /// <summary>The signer responsible for filling this field.</summary>
    public required string SignerId { get; set; }

    /// <summary>The field to place, matching a field ID in the document/template.</summary>
    public required string FieldId { get; set; }

    /// <summary>Placement and display settings using the API's required <c>left</c>, <c>top</c>, <c>width</c>, <c>height</c>, and <c>fontSize</c> values.</summary>
    public DisplaySettings? DisplaySettings { get; set; }
}

/// <summary>Cost breakdown for creating an assignment plus the account's current balances, returned by the estimate-cost endpoint.</summary>
public sealed record AssignmentCostEstimate
{
    /// <summary>Documents consumed from the plan allowance (always 1).</summary>
    [JsonPropertyName("documents")]
    public int Documents { get; init; }

    /// <summary>Total notification credits needed.</summary>
    [JsonPropertyName("credits")]
    public decimal Credits { get; init; }

    /// <summary><see langword="true"/> when the plan's document allowance is exhausted and an extra document is charged from credits.</summary>
    [JsonPropertyName("needs_extra_document")]
    public bool NeedsExtraDocument { get; init; }

    /// <summary>Credits charged for the extra document when <see cref="NeedsExtraDocument"/> is <see langword="true"/>.</summary>
    [JsonPropertyName("extra_document_cost")]
    public decimal ExtraDocumentCost { get; init; }

    /// <summary>Total credits required for the assignment (notifications plus any extra-document cost).</summary>
    [JsonPropertyName("total_credits")]
    public decimal TotalCredits { get; init; }

    /// <summary>Per-line-item cost breakdown.</summary>
    [JsonPropertyName("breakdown")]
    public IReadOnlyList<CostBreakdownItem> Breakdown { get; init; } = [];

    /// <summary>The account's current document balance.</summary>
    [JsonPropertyName("document_balance")]
    public decimal DocumentBalance { get; init; }

    /// <summary>The account's current credit balance.</summary>
    [JsonPropertyName("credit_balance")]
    public decimal CreditBalance { get; init; }

    /// <summary>Whether the account has enough documents and credits to create the assignment.</summary>
    [JsonPropertyName("has_sufficient_resources")]
    public bool HasSufficientResources { get; init; }

    /// <summary>Reason resources are insufficient (e.g. <c>PendingPayment</c>, <c>InsufficientDocuments</c>, <c>InsufficientCredits</c>), or <see langword="null"/> when the request can proceed.</summary>
    [JsonPropertyName("blocking_reason")]
    public string? BlockingReason { get; init; }

    /// <summary>Human-readable explanation accompanying the estimate, or <see langword="null"/>.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

/// <summary>Result of resending a signature-request notification to a signer.</summary>
public sealed record ResendNotificationResult
{
    /// <summary>Whether the notification was sent.</summary>
    [JsonPropertyName("is_sent")]
    public bool IsSent { get; init; }

    /// <summary>The document the signature request belongs to.</summary>
    [JsonPropertyName("document_id")]
    public string DocumentId { get; init; } = string.Empty;

    /// <summary>The signer the request was resent to.</summary>
    [JsonPropertyName("signer_id")]
    public string SignerId { get; init; } = string.Empty;
}

/// <summary>
/// Cost estimate for resending a signature request. It includes the current full-cost shape and
/// retains legacy fields returned by older deployments.
/// </summary>
public sealed record ResendCostEstimate
{
    /// <summary>Documents consumed from the plan allowance.</summary>
    [JsonPropertyName("documents")]
    public int Documents { get; init; }

    /// <summary>Total notification credits needed.</summary>
    [JsonPropertyName("credits")]
    public decimal Credits { get; init; }

    /// <summary>Whether an extra document must be charged from credits.</summary>
    [JsonPropertyName("needs_extra_document")]
    public bool NeedsExtraDocument { get; init; }

    /// <summary>Credits charged for an extra document.</summary>
    [JsonPropertyName("extra_document_cost")]
    public decimal ExtraDocumentCost { get; init; }

    /// <summary>Total credits required.</summary>
    [JsonPropertyName("total_credits")]
    public decimal TotalCredits { get; init; }

    /// <summary>Legacy total-credit field returned by older deployments.</summary>
    [JsonPropertyName("total")]
    public decimal Total { get; init; }

    /// <summary>Per-line-item cost breakdown.</summary>
    [JsonPropertyName("breakdown")]
    public IReadOnlyList<CostBreakdownItem> Breakdown { get; init; } = [];

    /// <summary>The account's current document balance.</summary>
    [JsonPropertyName("document_balance")]
    public decimal DocumentBalance { get; init; }

    /// <summary>The account's current credit balance.</summary>
    [JsonPropertyName("credit_balance")]
    public decimal CreditBalance { get; init; }

    /// <summary>Whether the account has enough documents and credits.</summary>
    [JsonPropertyName("has_sufficient_resources")]
    public bool HasSufficientResources { get; init; }

    /// <summary>Legacy sufficiency field returned by older deployments.</summary>
    [JsonPropertyName("has_sufficient_credits")]
    public bool HasSufficientCredits { get; init; }

    /// <summary>Machine-readable reason resources are insufficient, or <see langword="null"/>.</summary>
    [JsonPropertyName("blocking_reason")]
    public string? BlockingReason { get; init; }

    /// <summary>Human-readable explanation accompanying the estimate, or <see langword="null"/>.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

/// <summary>A single line item within a cost breakdown.</summary>
public sealed record CostBreakdownItem
{
    /// <summary>Machine-readable code identifying the charge.</summary>
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    /// <summary>Human-readable name of the charge.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Total cost of this line item, in credits.</summary>
    [JsonPropertyName("cost")]
    public decimal Cost { get; init; }

    /// <summary>Integer number of units charged, or <see langword="null"/> when omitted.</summary>
    [JsonPropertyName("quantity")]
    public int? Quantity { get; init; }

    /// <summary>Cost per unit, in credits, or <see langword="null"/>.</summary>
    [JsonPropertyName("unit_cost")]
    public decimal? UnitCost { get; init; }
}

/// <summary>A rendered WhatsApp notification sent for an assignment, split into header/body/buttons as the signer would see it.</summary>
public sealed record WhatsAppNotification
{
    /// <summary>Unix-epoch timestamp (seconds) when the message was sent.</summary>
    [JsonPropertyName("sent_at")]
    public long SentAt { get; init; }

    /// <summary>The rendered message header text.</summary>
    [JsonPropertyName("header")]
    public string Header { get; init; } = string.Empty;

    /// <summary>The rendered message body text.</summary>
    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;

    /// <summary>The interactive buttons shown with the message.</summary>
    [JsonPropertyName("buttons")]
    public IReadOnlyList<WhatsAppNotificationButton> Buttons { get; init; } = [];

    /// <summary>Recipient phone number in E.164 format.</summary>
    [JsonPropertyName("phone_number")]
    public string PhoneNumber { get; init; } = string.Empty;

    /// <summary>The signer the message was sent to.</summary>
    [JsonPropertyName("signer_id")]
    public string SignerId { get; init; } = string.Empty;
}

/// <summary>An interactive button within a <see cref="WhatsAppNotification"/>.</summary>
public sealed record WhatsAppNotificationButton
{
    /// <summary>The button label shown to the signer.</summary>
    [JsonPropertyName("text")]
    public string Text { get; init; } = string.Empty;

    /// <summary>The link the button opens (in sandbox/stage this includes access/verification codes for simulating the signing flow), or <see langword="null"/>.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }
}

/// <summary>Maps an existing signer to a template role when creating a document from a template. The signer must already exist in the account.</summary>
public sealed class TemplateSigner
{
    /// <summary>The template role ID this signer fills.</summary>
    public required string RoleId { get; set; }

    /// <summary>
    /// ID of the existing signer to assign to the role. Required when creating a document and
    /// omitted when estimating a template's cost.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>How the signer's identity is verified before signing: <c>Email</c>, <c>Whatsapp</c>, or <c>DigitalCertificate</c>. If a notification-capable channel is set without <see cref="NotificationMethods"/>, it is also used for notification.</summary>
    public string? VerificationMethod { get; set; }

    /// <summary>The single channel used to notify the signer: <c>Email</c> or <c>Whatsapp</c>. If set without <see cref="VerificationMethod"/>, it also determines the verification channel.</summary>
    public string[]? NotificationMethods { get; set; }

    /// <summary>Signing-order step. Signers sharing a step sign in parallel; the next step activates once the previous step completes. Omit for all-at-once signing.</summary>
    public int? Step { get; set; }
}
