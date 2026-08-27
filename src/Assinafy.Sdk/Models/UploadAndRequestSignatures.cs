using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>A signer to create for the <c>UploadAndRequestSignatures</c> convenience flow.</summary>
public sealed class UploadAndRequestSignaturesSigner
{
    /// <summary>The signer's full name (required).</summary>
    public required string FullName { get; set; }

    /// <summary>The signer's email address; required when verifying or notifying by <c>Email</c>.</summary>
    public string? Email { get; set; }

    /// <summary>The signer's WhatsApp phone number; required when verifying or notifying by <c>Whatsapp</c>.</summary>
    [JsonPropertyName("whatsapp_phone_number")]
    public string? WhatsAppPhoneNumber { get; set; }

    /// <summary>Optional per-signer verification method: <c>Email</c>, <c>Whatsapp</c>, or <c>DigitalCertificate</c>.</summary>
    public string? VerificationMethod { get; set; }

    /// <summary>Optional notification channels: <c>Email</c>, <c>Whatsapp</c>, or both.</summary>
    public string[]? NotificationMethods { get; set; }

    /// <summary>Optional signing-order step for this signer (see <see cref="SignerRef.Step"/>).</summary>
    public int? Step { get; set; }
}

/// <summary>
/// Options for the <c>UploadAndRequestSignatures</c> convenience flow, which uploads a file,
/// creates the signers, and opens an assignment in a single call.
/// </summary>
public sealed class UploadAndRequestSignaturesOptions
{
    /// <summary>The file content to upload (required).</summary>
    public required Stream FileStream { get; set; }

    /// <summary>The uploaded file's name (required).</summary>
    public required string FileName { get; set; }

    /// <summary>The signers to create for the assignment; at least one is required.</summary>
    public required IReadOnlyList<UploadAndRequestSignaturesSigner> Signers { get; set; }

    /// <summary>When <see langword="true"/> (the default), waits for the document to reach <c>ready</c> before creating the assignment.</summary>
    public bool? WaitForReady { get; set; }

    /// <summary>Assignment method to create. Defaults to <c>virtual</c> when not set.</summary>
    public string? Method { get; set; }

    /// <summary>Optional message sent to signers.</summary>
    public string? Message { get; set; }

    /// <summary>Optional assignment expiration as an ISO-8601 date-time string; no expiration by default.</summary>
    public string? ExpiresAt { get; set; }

    /// <summary>Optional existing signer IDs that receive a copy of the assignment.</summary>
    public string[]? CopyReceivers { get; set; }

    /// <summary>Field placements required when <see cref="Method"/> is <see cref="AssignmentMethods.Collect"/>.</summary>
    public IReadOnlyList<AssignmentEntry>? Entries { get; set; }

    /// <summary>
    /// Creates field placements from the newly created signer IDs, in the same order as
    /// <see cref="Signers"/>. Use this instead of <see cref="Entries"/> when collect entries
    /// must reference those IDs; the callback must return at least one entry.
    /// </summary>
    public Func<IReadOnlyList<string>, IReadOnlyList<AssignmentEntry>>? EntriesFactory { get; set; }

    /// <summary>Optional workspace account ID for this call; overrides the client's default account.</summary>
    public string? AccountId { get; set; }
}

/// <summary>Result of the <c>UploadAndRequestSignatures</c> convenience flow.</summary>
public sealed class UploadAndRequestSignaturesResult
{
    /// <summary>The uploaded document and its current lifecycle state.</summary>
    public required DocumentDetails Document { get; init; }

    /// <summary>The signature request (assignment) created for the document.</summary>
    public required Assignment Assignment { get; init; }

    /// <summary>The IDs of the signers created for the assignment.</summary>
    public required IReadOnlyList<string> SignerIds { get; init; }
}
