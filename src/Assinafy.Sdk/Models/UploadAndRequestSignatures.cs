using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

public sealed class UploadAndRequestSignaturesSigner
{
    public required string FullName { get; set; }
    public string? Email { get; set; }

    [JsonPropertyName("whatsapp_phone_number")]
    public string? WhatsAppPhoneNumber { get; set; }

    /// <summary>Optional per-signer verification method (e.g. <c>Email</c> or <c>Whatsapp</c>).</summary>
    public string? VerificationMethod { get; set; }

    /// <summary>Optional per-signer notification channels (e.g. <c>["Email"]</c> or <c>["Whatsapp"]</c>).</summary>
    public string[]? NotificationMethods { get; set; }

    /// <summary>Optional signing-order step for this signer (see <see cref="SignerRef.Step"/>).</summary>
    public int? Step { get; set; }
}

public sealed class UploadAndRequestSignaturesOptions
{
    public required Stream FileStream { get; set; }
    public required string FileName { get; set; }
    public required IReadOnlyList<UploadAndRequestSignaturesSigner> Signers { get; set; }
    public bool? WaitForReady { get; set; }

    /// <summary>Assignment method to create. Defaults to <c>virtual</c> when not set.</summary>
    public string? Method { get; set; }
    public string? Message { get; set; }
    public string? ExpiresAt { get; set; }
    public string[]? CopyReceivers { get; set; }
    public string? AccountId { get; set; }
}

public sealed class UploadAndRequestSignaturesResult
{
    public required DocumentDetails Document { get; init; }
    public required Assignment Assignment { get; init; }
    public required IReadOnlyList<string> SignerIds { get; init; }
}
