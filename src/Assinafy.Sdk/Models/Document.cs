using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>
/// One supported document status and whether documents in that status can be
/// deleted. Returned by <c>GET /v1/documents/statuses</c>.
/// </summary>
public sealed record DocumentStatusInfo
{
    /// <summary>
    /// Status code, one of <c>uploading</c>, <c>uploaded</c>, <c>metadata_processing</c>,
    /// <c>metadata_ready</c>, <c>expired</c>, <c>certificating</c>, <c>certificated</c>,
    /// <c>rejected_by_signer</c>, <c>pending_signature</c>, <c>rejected_by_user</c>, or <c>failed</c>.
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    /// <summary>Whether a document in this status can be deleted.</summary>
    [JsonPropertyName("deletable")]
    public bool Deletable { get; init; }
}

/// <summary>
/// Download URLs for a document's generated artifacts, keyed by artifact name
/// (see <see cref="DocumentArtifactNames"/>). Each URL is <see langword="null"/>
/// until the corresponding artifact has been produced.
/// </summary>
public sealed record DocumentArtifacts
{
    /// <summary>URL of the original uploaded PDF, or <see langword="null"/> if unavailable.</summary>
    [JsonPropertyName("original")]
    public string? Original { get; init; }

    /// <summary>URL of the signed and certificated PDF, or <see langword="null"/> if not yet produced.</summary>
    [JsonPropertyName("certificated")]
    public string? Certificated { get; init; }

    /// <summary>URL of the standalone certificate page, or <see langword="null"/> if not yet produced.</summary>
    [JsonPropertyName("certificate-page")]
    public string? CertificatePage { get; init; }

    /// <summary>URL of the signed PDF using the PAdES signature format, or <see langword="null"/> if unavailable.</summary>
    [JsonPropertyName("pades")]
    public string? Pades { get; init; }

    /// <summary>URL of the signed PDF bundled with the certificate page, or <see langword="null"/> if not yet produced.</summary>
    [JsonPropertyName("bundle")]
    public string? Bundle { get; init; }

    /// <summary>URL of the document's first-page thumbnail image, or <see langword="null"/> if unavailable.</summary>
    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; init; }
}

/// <summary>Shared page geometry returned for both document and template pages.</summary>
public abstract record PageBase
{
    /// <summary>Unique identifier of the page.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>1-based page number within the document.</summary>
    [JsonPropertyName("number")]
    public int Number { get; init; }

    /// <summary>Page height in pixels.</summary>
    [JsonPropertyName("height")]
    public int Height { get; init; }

    /// <summary>Page width in pixels.</summary>
    [JsonPropertyName("width")]
    public int Width { get; init; }

    /// <summary>URL to download the rendered page image, or <see langword="null"/> when unavailable.</summary>
    [JsonPropertyName("download_url")]
    public string? DownloadUrl { get; init; }
}

/// <summary>A single rendered page of a document.</summary>
public sealed record DocumentPage : PageBase;

/// <summary>A document and its current lifecycle state, as returned in list responses.</summary>
public record DocumentListItem
{
    /// <summary>Resource type name; present in single-resource responses.</summary>
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    /// <summary>Unique identifier of the document.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Identifier of the workspace account the document belongs to.</summary>
    [JsonPropertyName("account_id")]
    public string? AccountId { get; init; }

    /// <summary>Identifier of the template the document was created from, or <see langword="null"/> when uploaded directly.</summary>
    [JsonPropertyName("template_id")]
    public string? TemplateId { get; init; }

    /// <summary>Document name (title).</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Status code — see <see cref="DocumentStatusInfo"/> and <c>GET /v1/documents/statuses</c>.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>The signature request (signers, method, expiry) created for this document, or <see langword="null"/> before one exists.</summary>
    [JsonPropertyName("assignment")]
    public Assignment? Assignment { get; init; }

    /// <summary>Download URLs for the document's generated artifacts.</summary>
    [JsonPropertyName("artifacts")]
    public DocumentArtifacts? Artifacts { get; init; }

    /// <summary>The rendered pages of the document.</summary>
    [JsonPropertyName("pages")]
    public IReadOnlyList<DocumentPage> Pages { get; init; } = [];

    /// <summary>ISO-8601 date-time the document was created.</summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;

    /// <summary>ISO-8601 date-time the document was last updated, or <see langword="null"/>.</summary>
    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }

    /// <summary>Whether the document is closed.</summary>
    [JsonPropertyName("is_closed")]
    public bool IsClosed { get; init; }

    /// <summary>URL where the document can be signed, or <see langword="null"/> when not applicable.</summary>
    [JsonPropertyName("signing_url")]
    public string? SigningUrl { get; init; }

    /// <summary>Reason a signer gave for declining; only present when the access token belongs to the document's creator.</summary>
    [JsonPropertyName("decline_reason")]
    public string? DeclineReason { get; init; }

    /// <summary>The signer who declined the document, or <see langword="null"/> when it was not declined.</summary>
    [JsonPropertyName("declined_by")]
    public Signer? DeclinedBy { get; init; }

    /// <summary>Workspace tags attached to the document.</summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<Tag> Tags { get; init; } = [];
}

/// <summary>
/// Full document payload returned by single-document endpoints. Extends
/// <see cref="DocumentListItem"/> with the detail-only <c>activities</c> and
/// <c>current_signer</c> fields.
/// </summary>
public sealed record DocumentDetails : DocumentListItem
{
    /// <summary>The document's recorded activity (audit-log) entries, or <see langword="null"/> when not included.</summary>
    [JsonPropertyName("activities")]
    public IReadOnlyList<DocumentActivity>? Activities { get; init; }

    /// <summary>The signer whose turn it currently is to sign, or <see langword="null"/> when none applies.</summary>
    [JsonPropertyName("current_signer")]
    public AssignmentSigner? CurrentSigner { get; init; }
}

/// <summary>A single entry in a document's activity (audit) log.</summary>
public sealed record DocumentActivity
{
    /// <summary>Unique identifier of the activity entry.</summary>
    [JsonPropertyName("id")]
    public long Id { get; init; }

    /// <summary>Event type code.</summary>
    [JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    /// <summary>Human-readable message describing the event, or <see langword="null"/>.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>Event-specific payload snapshot; its keys vary per event.</summary>
    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }

    /// <summary>Request origin when available, carrying <c>ip</c> and <c>user-agent</c>.</summary>
    [JsonPropertyName("origin")]
    public JsonElement? Origin { get; init; }

    /// <summary>ISO-8601 date-time the activity was recorded.</summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;
}

/// <summary>Minimal document details exposed by the public (signer-facing) document endpoints.</summary>
public sealed record PublicDocumentInfo
{
    /// <summary>Resource type name; present in single-resource responses.</summary>
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    /// <summary>Unique identifier of the document.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Document name (title).</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Number of pages in the document, as a string.</summary>
    [JsonPropertyName("page_count")]
    public string? PageCount { get; init; }

    /// <summary>Display name of the document's creator, or <see langword="null"/>.</summary>
    [JsonPropertyName("created_by")]
    public string? CreatedBy { get; init; }
}

/// <summary>Result of sending a one-time access token for a public document.</summary>
public sealed record SendDocumentTokenResult
{
    /// <summary>The public document the token grants access to.</summary>
    [JsonPropertyName("document")]
    public PublicDocumentInfo? Document { get; init; }

    /// <summary>Delivery channel the token was sent over (<c>Email</c> or <c>Whatsapp</c> — see <see cref="SignerChannels"/>).</summary>
    [JsonPropertyName("channel")]
    public string Channel { get; init; } = string.Empty;

    /// <summary>The address the token was sent to (email address or phone number).</summary>
    [JsonPropertyName("recipient")]
    public string Recipient { get; init; } = string.Empty;
}

/// <summary>
/// Certification details returned when verifying a signed document by its signature hash.
/// The call always returns <c>200</c>: when the hash is unknown or the document is not signed,
/// <see cref="IsValid"/> is <see langword="false"/>, the other fields are <see langword="null"/>,
/// and <see cref="Message"/> explains why.
/// </summary>
public sealed record DocumentVerificationResult
{
    /// <summary>The document signature hash that was looked up.</summary>
    [JsonPropertyName("hash")]
    public string Hash { get; init; } = string.Empty;

    /// <summary>Identifier of the matched document, or <see langword="null"/> when the hash was not found.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    /// <summary>Status code of the matched document, or <see langword="null"/> when not valid.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>Number of pages in the document, as a string.</summary>
    [JsonPropertyName("page_count")]
    public string? PageCount { get; init; }

    /// <summary>Total number of signers, as a string.</summary>
    [JsonPropertyName("signer_count")]
    public string? SignerCount { get; init; }

    /// <summary>Number of signers who have completed signing.</summary>
    [JsonPropertyName("completed_count")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? CompletedCount { get; init; }

    /// <summary>ISO-8601 date-time signing was completed, or <see langword="null"/>.</summary>
    [JsonPropertyName("completed_at")]
    public string? CompletedAt { get; init; }

    /// <summary>ISO-8601 date-time the document was verified, or <see langword="null"/>.</summary>
    [JsonPropertyName("verified_at")]
    public string? VerifiedAt { get; init; }

    /// <summary>Whether the hash resolves to a valid, signed document.</summary>
    [JsonPropertyName("is_valid")]
    public bool IsValid { get; init; }

    /// <summary>Explanatory reason when the document is not valid.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

/// <summary>A convenience summary of a document's signing progress, computed by the SDK from its signers.</summary>
public sealed record SigningProgress
{
    /// <summary>Number of signers who have signed.</summary>
    public int Signed { get; init; }

    /// <summary>Total number of signers.</summary>
    public int Total { get; init; }

    /// <summary>Proportion signed, from 0 to 100.</summary>
    public double Percentage { get; init; }

    /// <summary>Number of signers who have not yet signed.</summary>
    public int Pending { get; init; }
}

/// <summary>
/// Options for creating a document from a template
/// (<c>POST /v1/accounts/{account_id}/templates/{template_id}/documents</c>).
/// </summary>
public sealed class CreateDocumentFromTemplateOptions
{
    /// <summary>Title for the new document; defaults to the template name.</summary>
    public string? Name { get; set; }

    /// <summary>Optional message sent to signers.</summary>
    public string? Message { get; set; }

    /// <summary>Assignment expiration as an ISO-8601 date-time; no expiration by default.</summary>
    public string? ExpiresAt { get; set; }

    /// <summary>Editor field values to bake into the generated document.</summary>
    public IReadOnlyList<TemplateEditorField>? EditorFields { get; set; }

    /// <summary>
    /// Tag names to attach. Missing names are created, then merged with the template's default tags.
    /// </summary>
    public IReadOnlyList<string>? Tags { get; set; }
}

/// <summary>A single editor field value to bake into a document generated from a template.</summary>
public sealed class TemplateEditorField
{
    /// <summary>The field identifier, matching the <c>field_id</c> in the template data.</summary>
    public required string FieldId { get; set; }

    /// <summary>The required string value to assign to the field.</summary>
    public required string Value { get; set; }
}

/// <summary>
/// Request to send a one-time access token for a public document
/// (<c>PUT /v1/public/documents/{document_id}/send-token</c>).
/// </summary>
public sealed class SendDocumentTokenRequest
{
    /// <summary>The address to send the token to (email address or phone number).</summary>
    public required string Recipient { get; set; }

    /// <summary>Delivery channel (<c>Email</c> or <c>Whatsapp</c> — see <see cref="SignerChannels"/>).</summary>
    public required string Channel { get; set; }
}
