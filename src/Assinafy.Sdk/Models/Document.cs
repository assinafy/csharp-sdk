using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

public sealed record DocumentStatusInfo
{
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    [JsonPropertyName("deletable")]
    public bool Deletable { get; init; }
}

public sealed record DocumentArtifacts
{
    [JsonPropertyName("original")]
    public string? Original { get; init; }

    [JsonPropertyName("certificated")]
    public string? Certificated { get; init; }

    [JsonPropertyName("certificate-page")]
    public string? CertificatePage { get; init; }

    [JsonPropertyName("bundle")]
    public string? Bundle { get; init; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; init; }
}

public sealed record DocumentPage
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("number")]
    public int Number { get; init; }

    [JsonPropertyName("height")]
    public double Height { get; init; }

    [JsonPropertyName("width")]
    public double Width { get; init; }

    [JsonPropertyName("download_url")]
    public string? DownloadUrl { get; init; }
}

public sealed record DocumentListItem
{
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("account_id")]
    public string? AccountId { get; init; }

    [JsonPropertyName("template_id")]
    public string? TemplateId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("assignment")]
    public Assignment? Assignment { get; init; }

    [JsonPropertyName("artifacts")]
    public DocumentArtifacts? Artifacts { get; init; }

    [JsonPropertyName("pages")]
    public IReadOnlyList<DocumentPage> Pages { get; init; } = [];

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }

    [JsonPropertyName("is_closed")]
    public bool IsClosed { get; init; }

    [JsonPropertyName("signing_url")]
    public string? SigningUrl { get; init; }

    [JsonPropertyName("decline_reason")]
    public string? DeclineReason { get; init; }

    [JsonPropertyName("declined_by")]
    public Signer? DeclinedBy { get; init; }
}

public sealed record DocumentDetails
{
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("account_id")]
    public string? AccountId { get; init; }

    [JsonPropertyName("template_id")]
    public string? TemplateId { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("assignment")]
    public Assignment? Assignment { get; init; }

    [JsonPropertyName("artifacts")]
    public DocumentArtifacts? Artifacts { get; init; }

    [JsonPropertyName("pages")]
    public IReadOnlyList<DocumentPage> Pages { get; init; } = [];

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }

    [JsonPropertyName("is_closed")]
    public bool IsClosed { get; init; }

    [JsonPropertyName("signing_url")]
    public string? SigningUrl { get; init; }

    [JsonPropertyName("decline_reason")]
    public string? DeclineReason { get; init; }

    [JsonPropertyName("declined_by")]
    public Signer? DeclinedBy { get; init; }

    [JsonPropertyName("activities")]
    public IReadOnlyList<DocumentActivity>? Activities { get; init; }

    [JsonPropertyName("current_signer")]
    public AssignmentSigner? CurrentSigner { get; init; }
}

public sealed record DocumentActivity
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }

    [JsonPropertyName("origin")]
    public JsonElement? Origin { get; init; }

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;
}

public sealed record PublicDocumentInfo
{
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("page_count")]
    public string? PageCount { get; init; }

    [JsonPropertyName("created_by")]
    public string? CreatedBy { get; init; }
}

public sealed record SendDocumentTokenResult
{
    [JsonPropertyName("document")]
    public PublicDocumentInfo? Document { get; init; }

    [JsonPropertyName("channel")]
    public string Channel { get; init; } = string.Empty;

    [JsonPropertyName("recipient")]
    public string Recipient { get; init; } = string.Empty;
}

public sealed record DocumentVerificationResult
{
    [JsonPropertyName("hash")]
    public string Hash { get; init; } = string.Empty;

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("page_count")]
    public string? PageCount { get; init; }

    [JsonPropertyName("signer_count")]
    public string? SignerCount { get; init; }

    [JsonPropertyName("completed_count")]
    public int? CompletedCount { get; init; }

    [JsonPropertyName("completed_at")]
    public string? CompletedAt { get; init; }

    [JsonPropertyName("verified_at")]
    public string? VerifiedAt { get; init; }

    [JsonPropertyName("is_valid")]
    public bool IsValid { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }
}

public sealed record SigningProgress
{
    public int Signed { get; init; }
    public int Total { get; init; }
    public double Percentage { get; init; }
    public int Pending { get; init; }
}

public sealed class CreateDocumentFromTemplateOptions
{
    public string? Name { get; set; }
    public string? Message { get; set; }
    public string? ExpiresAt { get; set; }
    public IReadOnlyList<TemplateEditorField>? EditorFields { get; set; }
}

public sealed class TemplateEditorField
{
    public required string FieldId { get; set; }
    public object? Value { get; set; }
}

public sealed class SendDocumentTokenRequest
{
    public required string Recipient { get; set; }
    public required string Channel { get; set; }
}
