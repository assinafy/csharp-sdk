using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

public record Signer
{
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    public string? Email { get; init; }

    [JsonPropertyName("whatsapp_phone_number")]
    public string? WhatsAppPhoneNumber { get; init; }

    [JsonPropertyName("has_accepted_terms")]
    public bool HasAcceptedTerms { get; init; }

    [JsonPropertyName("has_signature")]
    public bool? HasSignature { get; init; }

    [JsonPropertyName("has_initial")]
    public bool? HasInitial { get; init; }
}

/// <summary>Body for <c>POST /accounts/{account_id}/signers</c>.</summary>
public sealed class CreateSignerRequest
{
    public required string FullName { get; set; }
    public string? Email { get; set; }

    [JsonPropertyName("whatsapp_phone_number")]
    public string? WhatsAppPhoneNumber { get; set; }
}

/// <summary>Body for <c>PUT /accounts/{account_id}/signers/{signer_id}</c>.</summary>
public sealed class UpdateSignerRequest
{
    public string? FullName { get; set; }
    public string? Email { get; set; }

    [JsonPropertyName("whatsapp_phone_number")]
    public string? WhatsAppPhoneNumber { get; set; }
}

/// <summary>Body for <c>PUT /documents/{document_id}/signers/confirm-data</c>.</summary>
public sealed class ConfirmSignerDataRequest
{
    public string? Email { get; set; }

    [JsonPropertyName("whatsapp_phone_number")]
    public string? WhatsAppPhoneNumber { get; set; }

    public bool? HasAcceptedTerms { get; set; }
}

public sealed record VerifyEmailResult
{
    [JsonPropertyName("is_email_verified")]
    public bool IsEmailVerified { get; init; }

    [JsonPropertyName("email")]
    public string? Email { get; init; }
}
