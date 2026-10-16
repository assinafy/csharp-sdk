using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>Current signer identity-document verification state from <c>GET /signers/identity-verification</c>.</summary>
public sealed record IdentityVerificationStatus
{
    /// <summary>Whether no session exists or the previous session expired before submission, allowing a new session.</summary>
    [JsonPropertyName("can_start")]
    public bool CanStart { get; init; }

    /// <summary>Pending, InReview, Approved, Declined, Expired, Abandoned, or null before a session exists.</summary>
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    /// <summary>Capture URL while Pending; otherwise null. Open as-is, including its fragment; never log or persist this bearer link.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>ISO-8601 capture-link expiration, or null when the provider does not report it.</summary>
    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; init; }

    /// <summary>ISO-8601 completion time, or null while incomplete.</summary>
    [JsonPropertyName("completed_at")]
    public string? CompletedAt { get; init; }
}

/// <summary>A hosted capture session from <c>POST /signers/identity-verification</c>.</summary>
public sealed record IdentityVerificationSession
{
    /// <summary>Session identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Hosted capture URL. Open as-is, including its fragment; never log or persist this bearer link.</summary>
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    /// <summary>ISO-8601 capture-link expiration, or null when not reported.</summary>
    [JsonPropertyName("expires_at")]
    public string? ExpiresAt { get; init; }
}

/// <summary>Account-facing identity review summary. Raw evidence images are available only while InReview.</summary>
public sealed record SignerIdentityReview
{
    /// <summary>Verification identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Pending, InReview, Approved, Declined, Expired, or Abandoned.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>Verification provider, currently AssinafyVerify.</summary>
    [JsonPropertyName("provider")]
    public string Provider { get; init; } = string.Empty;

    /// <summary>Verification method, currently IdDocumentFaceMatchBasic.</summary>
    [JsonPropertyName("verification_method")]
    public string VerificationMethod { get; init; } = string.Empty;

    /// <summary>Reasons for review, in provider order; unknown codes retain their code as the description.</summary>
    [JsonPropertyName("reason_codes")]
    public IReadOnlyList<IdentityReviewReason> ReasonCodes { get; init; } = [];

    /// <summary>Whether Assinafy has fetched the provider's evidence summary.</summary>
    [JsonPropertyName("evidence_available")]
    public bool EvidenceAvailable { get; init; }

    /// <summary>Registered signer identity, or null when no summary is available.</summary>
    [JsonPropertyName("expected")]
    public IdentityReviewPerson? Expected { get; init; }

    /// <summary>Identity read from the document, or null when unavailable.</summary>
    [JsonPropertyName("document")]
    public IdentityReviewDocument? Document { get; init; }

    /// <summary>Identity matches and similarity scores, or null when unavailable.</summary>
    [JsonPropertyName("matches")]
    public IdentityReviewMatches? Matches { get; init; }

    /// <summary>Provider checks and their results.</summary>
    [JsonPropertyName("checks")]
    public IReadOnlyList<IdentityReviewCheck> Checks { get; init; } = [];

    /// <summary>Available evidence names in display order: document_front, document_back, document_pdf, face. Empty after a decision.</summary>
    [JsonPropertyName("images")]
    public IReadOnlyList<string> Images { get; init; } = [];

    /// <summary>Provider ruleset version, or null when unavailable.</summary>
    [JsonPropertyName("ruleset_version")]
    public string? RulesetVersion { get; init; }

    /// <summary>SHA-256 of the evidence summary, or null when unavailable.</summary>
    [JsonPropertyName("evidence_sha256")]
    public string? EvidenceSha256 { get; init; }

    /// <summary>Account user who decided the review, or null before review.</summary>
    [JsonPropertyName("reviewed_by")]
    public IdentityReviewer? ReviewedBy { get; init; }

    /// <summary>ISO-8601 decision time, or null before review.</summary>
    [JsonPropertyName("reviewed_at")]
    public string? ReviewedAt { get; init; }

    /// <summary>Private reviewer note; never shown to the signer.</summary>
    [JsonPropertyName("review_note")]
    public string? ReviewNote { get; init; }

    /// <summary>ISO-8601 completion time, or null while incomplete.</summary>
    [JsonPropertyName("completed_at")]
    public string? CompletedAt { get; init; }
}

/// <summary>One provider reason for sending an identity verification to review.</summary>
public sealed record IdentityReviewReason
{
    /// <summary>Machine-readable reason code.</summary>
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    /// <summary>Human-readable description.</summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}

/// <summary>Signer identity registered for verification.</summary>
public record IdentityReviewPerson
{
    /// <summary>Full name, or null when unavailable.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>CPF, or null when unavailable.</summary>
    [JsonPropertyName("cpf")]
    public string? Cpf { get; init; }
}

/// <summary>Identity read from the submitted government document.</summary>
public sealed record IdentityReviewDocument : IdentityReviewPerson
{
    /// <summary>Document type, such as CNH, or null when unavailable.</summary>
    [JsonPropertyName("type")]
    public string? Type { get; init; }
}

/// <summary>Identity comparisons between registered information and the document.</summary>
public sealed record IdentityReviewMatches
{
    /// <summary>Whether the CPF matches, or null when not checked.</summary>
    [JsonPropertyName("cpf")]
    public bool? Cpf { get; init; }

    /// <summary>Name similarity from zero to one, or null when not checked.</summary>
    [JsonPropertyName("name")]
    public double? Name { get; init; }

    /// <summary>Whether the birth date matches, or null when not checked.</summary>
    [JsonPropertyName("birth_date")]
    public bool? BirthDate { get; init; }
}

/// <summary>A provider check performed during identity verification.</summary>
public sealed record IdentityReviewCheck
{
    /// <summary>Check name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>pass, review, skipped, or a failure result.</summary>
    [JsonPropertyName("result")]
    public string Result { get; init; } = string.Empty;

    /// <summary>Score, or null when the check has no numeric score.</summary>
    [JsonPropertyName("score")]
    public double? Score { get; init; }

    /// <summary>Machine-readable reasons for the result.</summary>
    [JsonPropertyName("reasons")]
    public IReadOnlyList<string> Reasons { get; init; } = [];
}

/// <summary>The account user who decided an identity review.</summary>
public sealed record IdentityReviewer
{
    /// <summary>User identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>User display name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

/// <summary>Body for the account-facing identity-review decision endpoint.</summary>
public sealed class DecideIdentityReviewRequest
{
    /// <summary>Exactly Approved or Rejected. Rejection cancels the document and notifies signers and copy receivers.</summary>
    public required string Decision { get; set; }

    /// <summary>Optional note, at most 2000 characters; recorded in activity and sent to the provider, never shown to the signer.</summary>
    public string? Note { get; set; }
}
