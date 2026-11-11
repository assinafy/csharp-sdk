using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>Documents sent together, each keeping its own assignment, status and certificate.</summary>
public sealed record DocumentEnvelope
{
    /// <summary>Resource discriminator, present on single-envelope responses.</summary>
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    /// <summary>Envelope identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Name used in invitations.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Invitation message, or null.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>Documents in envelope order.</summary>
    [JsonPropertyName("documents")]
    public IReadOnlyList<EnvelopeDocument> Documents { get; init; } = [];

    /// <summary>Document counts by status; null when no documents remain.</summary>
    [JsonPropertyName("counts")]
    public IReadOnlyDictionary<string, int>? Counts { get; init; }

    /// <summary>One signing link per signer. Embedded signers have no URL.</summary>
    [JsonPropertyName("signing_urls")]
    public IReadOnlyList<SigningUrl> SigningUrls { get; init; } = [];

    /// <summary>ISO-8601 creation timestamp.</summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;

    /// <summary>Returns a representation without signing links.</summary>
    public override string ToString() => $"{nameof(DocumentEnvelope)} {{ <redacted> }}";
}

/// <summary>A document listed in an envelope.</summary>
public sealed record EnvelopeDocument
{
    /// <summary>Document identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Position in the envelope, starting at one.</summary>
    [JsonPropertyName("position")]
    public int Position { get; init; }

    /// <summary>Document name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Current document status.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>Artifact URLs. Lists include only the thumbnail URL, which can return 404 before generation.</summary>
    [JsonPropertyName("artifacts")]
    public IReadOnlyDictionary<string, string> Artifacts { get; init; } = new Dictionary<string, string>();

    /// <summary>Only the signers assigned to this document.</summary>
    [JsonPropertyName("signers")]
    public IReadOnlyList<EnvelopeDocumentSigner> Signers { get; init; } = [];
}

/// <summary>A signer's progress on one envelope document.</summary>
public sealed record EnvelopeDocumentSigner : Signer
{
    /// <summary>Whether the signer has signed this document.</summary>
    [JsonPropertyName("completed")]
    public bool Completed { get; init; }

    /// <summary>ISO-8601 signing time; null until signed.</summary>
    [JsonPropertyName("signed_at")]
    public string? SignedAt { get; init; }
}

/// <summary>Body for creating or estimating an envelope. One document allowance is charged per envelope.</summary>
public sealed class CreateEnvelopeRequest
{
    /// <summary>Invitation name, one to 255 characters, without a less-than sign.</summary>
    public required string Name { get; set; }

    /// <summary>Optional invitation message.</summary>
    public string? Message { get; set; }

    /// <summary>One to twenty distinct existing signers. Every signer must occur on a document; envelopes have no signing steps.</summary>
    public required IReadOnlyList<EnvelopeSignerRequest> Signers { get; set; }

    /// <summary>One to twenty processed documents without assignments, in invitation order.</summary>
    public required IReadOnlyList<EnvelopeDocumentRequest> Documents { get; set; }

    /// <summary>Nonnegative credit ceiling. Creation above it returns 409 CreditCeilingExceeded without charging; estimates ignore it.</summary>
    public decimal? MaxTotalCredits { get; set; }
}

/// <summary>One existing signer of an envelope.</summary>
public sealed class EnvelopeSignerRequest
{
    /// <summary>Existing workspace signer ID.</summary>
    public required string Id { get; set; }

    /// <summary>Email, Whatsapp, DigitalCertificate or IdDocumentFaceMatchBasic. Certificate signers sign virtual documents alone.</summary>
    public string? VerificationMethod { get; set; }

    /// <summary>Exactly one compatible Email or Whatsapp channel; omitted channels are inferred.</summary>
    public IReadOnlyList<string>? NotificationMethods { get; set; }

    /// <summary>Whether the signer uses an embed session in your application, including documents added later.</summary>
    public bool? Embedded { get; set; }
}

/// <summary>An uploaded, metadata_ready document to include in an envelope.</summary>
public sealed class EnvelopeDocumentRequest
{
    /// <summary>Existing workspace document ID; the document must not have an assignment.</summary>
    public required string Id { get; set; }

    /// <summary>Distinct IDs of envelope signers assigned to this document.</summary>
    public required IReadOnlyList<string> SignerIds { get; set; }

    /// <summary>virtual (default) or collect. Certificate signers require virtual.</summary>
    public string? Method { get; set; }

    /// <summary>Page fields required for collect, covering exactly this document's signers.</summary>
    public IReadOnlyList<AssignmentEntry>? Entries { get; set; }

    /// <summary>ISO-8601 expiration, at least one hour ahead, or null for none. Numeric text is accepted as Unix seconds.</summary>
    public string? ExpiresAt { get; set; }

    /// <summary>Existing signer IDs that receive only a certified copy.</summary>
    public IReadOnlyList<string>? CopyReceiverIds { get; set; }
}

/// <summary>An envelope's activity entry, including the document it belongs to.</summary>
public sealed record EnvelopeActivity
{
    /// <summary>Activity identifier.</summary>
    [JsonPropertyName("id")]
    public long Id { get; init; }

    /// <summary>Event code.</summary>
    [JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    /// <summary>Human-readable event message.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>Event-specific object, array or null.</summary>
    [JsonPropertyName("payload")]
    public JsonElement? Payload { get; init; }

    /// <summary>Request origin, when available.</summary>
    [JsonPropertyName("origin")]
    public JsonElement? Origin { get; init; }

    /// <summary>ISO-8601 activity timestamp.</summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;

    /// <summary>Document identifier.</summary>
    [JsonPropertyName("document_id")]
    public string DocumentId { get; init; } = string.Empty;

    /// <summary>Document name.</summary>
    [JsonPropertyName("document_name")]
    public string DocumentName { get; init; } = string.Empty;
}

/// <summary>Result for one document when an envelope is cancelled.</summary>
public sealed record EnvelopeCancellationResult
{
    /// <summary>Document identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Document status after the cancellation request.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>Whether this call cancelled the document. Already closed or certifying documents remain unchanged.</summary>
    [JsonPropertyName("cancelled")]
    public bool Cancelled { get; init; }
}

/// <summary>Result of resending one signer's envelope invitation.</summary>
public sealed record EnvelopeResendResult
{
    /// <summary>Whether every notification method sent the reminder.</summary>
    [JsonPropertyName("is_sent")]
    public bool IsSent { get; init; }

    /// <summary>Envelope identifier.</summary>
    [JsonPropertyName("envelope_id")]
    public string EnvelopeId { get; init; } = string.Empty;

    /// <summary>Signer identifier.</summary>
    [JsonPropertyName("signer_id")]
    public string SignerId { get; init; } = string.Empty;
}
