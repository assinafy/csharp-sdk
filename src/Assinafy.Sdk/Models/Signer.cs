using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>A signing party belonging to a workspace account.</summary>
public record Signer
{
    /// <summary>Resource type marker; present in single-resource responses.</summary>
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    /// <summary>Unique signer identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Signer's full name.</summary>
    [JsonPropertyName("full_name")]
    public string FullName { get; init; } = string.Empty;

    /// <summary>Signer's email address, or <see langword="null"/> when not set.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    /// <summary>Government-issued identity number, or <see langword="null"/> when not set.</summary>
    [JsonPropertyName("government_id")]
    public string? GovernmentId { get; init; }

    /// <summary>Signer's WhatsApp number in E.164 format (normalized on save), or <see langword="null"/> when not set.</summary>
    [JsonPropertyName("whatsapp_phone_number")]
    public string? WhatsAppPhoneNumber { get; init; }

    /// <summary>Whether the signer has accepted the terms of use.</summary>
    [JsonPropertyName("has_accepted_terms")]
    public bool HasAcceptedTerms { get; init; }

    /// <summary>Whether the signer has a saved signature image stored (returned by <c>GET /signers/self</c>), or <see langword="null"/> on endpoints that do not return the flag.</summary>
    [JsonPropertyName("has_signature")]
    public bool? HasSignature { get; init; }

    /// <summary>Whether the signer has a saved initials image stored (returned by <c>GET /signers/self</c>), or <see langword="null"/> on endpoints that do not return the flag.</summary>
    [JsonPropertyName("has_initial")]
    public bool? HasInitial { get; init; }

    /// <summary>
    /// Whether the signer opted to reuse their saved signature/initials in future processes,
    /// returned by <c>GET /signers/self</c>. When <see langword="false"/>, clients should not
    /// pre-render the saved image even if <see cref="HasSignature"/>/<see cref="HasInitial"/> is
    /// <see langword="true"/>. <see langword="null"/> on endpoints that do not return the flag.
    /// </summary>
    [JsonPropertyName("is_signature_reusable")]
    public bool? IsSignatureReusable { get; init; }
}

/// <summary>Body for <c>POST /accounts/{account_id}/signers</c>.</summary>
public sealed class CreateSignerRequest
{
    /// <summary>Signer's full name. Required.</summary>
    public required string FullName { get; set; }

    /// <summary>Signer's email address, or <see langword="null"/> to omit.</summary>
    public string? Email { get; set; }

    /// <summary>Signer's WhatsApp number in E.164 format (normalized on save), or <see langword="null"/> to omit.</summary>
    [JsonPropertyName("whatsapp_phone_number")]
    public string? WhatsAppPhoneNumber { get; set; }
}

/// <summary>Body for <c>PUT /accounts/{account_id}/signers/{signer_id}</c>.</summary>
public sealed class UpdateSignerRequest
{
    /// <summary>New full name, or <see langword="null"/> to leave unchanged.</summary>
    public string? FullName { get; set; }

    /// <summary>New email address, or <see langword="null"/> to leave unchanged. Cannot be changed while the signer has verified email on an in-flight (not yet certificated) document.</summary>
    public string? Email { get; set; }

    /// <summary>New government-issued identity number, or <see langword="null"/> to leave unchanged.</summary>
    public string? GovernmentId { get; set; }

    /// <summary>New WhatsApp number in E.164 format (normalized on save), or <see langword="null"/> to leave unchanged. Cannot be changed while the signer has verified WhatsApp on an in-flight (not yet certificated) document.</summary>
    [JsonPropertyName("whatsapp_phone_number")]
    public string? WhatsAppPhoneNumber { get; set; }
}

/// <summary>Body for <c>PUT /documents/{document_id}/signers/confirm-data</c>.</summary>
public sealed class ConfirmSignerDataRequest
{
    /// <summary>Signer's full name to confirm or supply, or <see langword="null"/> to omit.</summary>
    public string? FullName { get; set; }

    /// <summary>Signer's email address to confirm or supply, or <see langword="null"/> to omit.</summary>
    public string? Email { get; set; }

    /// <summary>Signer's government-issued identity number to confirm or supply, or <see langword="null"/> to omit.</summary>
    public string? GovernmentId { get; set; }

    /// <summary>Compatibility field accepted by older deployments for confirming the WhatsApp number.</summary>
    [JsonPropertyName("whatsapp_phone_number")]
    public string? WhatsAppPhoneNumber { get; set; }

    /// <summary>
    /// Compatibility terms flag. The published schema omits it, but the production signing guide
    /// still requires it for digital-certificate confirmation.
    /// </summary>
    public bool? HasAcceptedTerms { get; set; }
}

/// <summary>Legacy verification response model retained for compatibility. The current verification endpoint returns an envelope without data.</summary>
public sealed record VerifyEmailResult
{
    /// <summary>Whether the signer's email address is verified.</summary>
    [JsonPropertyName("is_email_verified")]
    public bool IsEmailVerified { get; init; }

    /// <summary>The signer's email address, or <see langword="null"/> when not present.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }
}
