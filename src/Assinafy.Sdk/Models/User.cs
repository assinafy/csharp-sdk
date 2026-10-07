using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>
/// The authenticated user's owner-facing email notification preferences. The API returns
/// all nine properties; <see langword="true"/> means the corresponding email is enabled.
/// </summary>
public sealed record NotificationPreferences
{
    /// <summary>Email when every signer has signed and the document is certified.</summary>
    [JsonPropertyName("DocumentCompleted")]
    public bool DocumentCompleted { get; init; }

    /// <summary>Email when a signer declines to sign.</summary>
    [JsonPropertyName("SignerDeclined")]
    public bool SignerDeclined { get; init; }

    /// <summary>Email when a document is cancelled.</summary>
    [JsonPropertyName("DocumentCancelled")]
    public bool DocumentCancelled { get; init; }

    /// <summary>Email when a signature deadline is approaching.</summary>
    [JsonPropertyName("DocumentAboutToExpire")]
    public bool DocumentAboutToExpire { get; init; }

    /// <summary>Email when a signature deadline passes.</summary>
    [JsonPropertyName("DocumentExpired")]
    public bool DocumentExpired { get; init; }

    /// <summary>Email when a signature deadline is extended.</summary>
    [JsonPropertyName("DocumentExpirationReset")]
    public bool DocumentExpirationReset { get; init; }

    /// <summary>Email when an uploaded document cannot be processed.</summary>
    [JsonPropertyName("DocumentProcessingFailed")]
    public bool DocumentProcessingFailed { get; init; }

    /// <summary>Email when a template cannot be processed.</summary>
    [JsonPropertyName("TemplateProcessingFailed")]
    public bool TemplateProcessingFailed { get; init; }

    /// <summary>Email when a WhatsApp notification to a signer cannot be delivered.</summary>
    [JsonPropertyName("SignerWhatsappFailed")]
    public bool SignerWhatsappFailed { get; init; }
}

/// <summary>
/// Body for <c>PUT /users/self/notification-preferences</c>. Only non-null properties are
/// sent, so omitted preferences retain their existing values.
/// </summary>
public sealed class UpdateNotificationPreferencesRequest
{
    /// <summary>Enable or disable the document-completed email.</summary>
    [JsonPropertyName("DocumentCompleted")]
    public bool? DocumentCompleted { get; set; }

    /// <summary>Enable or disable the signer-declined email.</summary>
    [JsonPropertyName("SignerDeclined")]
    public bool? SignerDeclined { get; set; }

    /// <summary>Enable or disable the document-cancelled email.</summary>
    [JsonPropertyName("DocumentCancelled")]
    public bool? DocumentCancelled { get; set; }

    /// <summary>Enable or disable the document-about-to-expire email.</summary>
    [JsonPropertyName("DocumentAboutToExpire")]
    public bool? DocumentAboutToExpire { get; set; }

    /// <summary>Enable or disable the document-expired email.</summary>
    [JsonPropertyName("DocumentExpired")]
    public bool? DocumentExpired { get; set; }

    /// <summary>Enable or disable the document-expiration-reset email.</summary>
    [JsonPropertyName("DocumentExpirationReset")]
    public bool? DocumentExpirationReset { get; set; }

    /// <summary>Enable or disable the document-processing-failed email.</summary>
    [JsonPropertyName("DocumentProcessingFailed")]
    public bool? DocumentProcessingFailed { get; set; }

    /// <summary>Enable or disable the template-processing-failed email.</summary>
    [JsonPropertyName("TemplateProcessingFailed")]
    public bool? TemplateProcessingFailed { get; set; }

    /// <summary>Enable or disable the signer-WhatsApp-failed email.</summary>
    [JsonPropertyName("SignerWhatsappFailed")]
    public bool? SignerWhatsappFailed { get; set; }
}

/// <summary>The authenticated user's enrolled two-factor methods (<c>GET /users/self/mfa</c>).</summary>
public sealed record MfaMethodList
{
    /// <summary>Enrolled two-factor methods.</summary>
    [JsonPropertyName("methods")]
    public IReadOnlyList<MfaMethod> Methods { get; init; } = [];

    /// <summary>Number of unused recovery codes.</summary>
    [JsonPropertyName("recovery_codes_remaining")]
    public int RecoveryCodesRemaining { get; init; }
}

/// <summary>An enrolled two-factor method.</summary>
public sealed record MfaMethod
{
    /// <summary>Method ID, used by <c>DeleteMfaMethodAsync</c>.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Method type (e.g. <c>Totp</c>).</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>User-chosen label, or <see langword="null"/>.</summary>
    [JsonPropertyName("label")]
    public string? Label { get; init; }

    /// <summary>ISO-8601 date-time when the enrollment was confirmed, or <see langword="null"/> while unconfirmed.</summary>
    [JsonPropertyName("confirmed_at")]
    public string? ConfirmedAt { get; init; }

    /// <summary>ISO-8601 date-time of the last successful use, or <see langword="null"/>.</summary>
    [JsonPropertyName("last_used_at")]
    public string? LastUsedAt { get; init; }
}

/// <summary>An unconfirmed authenticator enrollment (<c>POST /users/self/mfa/totp</c>). The secret is returned only once.</summary>
public sealed record TotpEnrollment
{
    /// <summary>Method ID to confirm with <c>ConfirmTotpEnrollmentAsync</c>.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Base32 shared secret for manual entry in an authenticator app.</summary>
    [JsonPropertyName("secret")]
    public string Secret { get; init; } = string.Empty;

    /// <summary><c>otpauth://</c> URI to render as a QR code.</summary>
    [JsonPropertyName("provisioning_uri")]
    public string ProvisioningUri { get; init; } = string.Empty;
}

/// <summary>A fresh set of recovery codes, shown only once.</summary>
public sealed record MfaRecoveryCodes
{
    /// <summary>The recovery codes (e.g. <c>ABCD-EFGH-JKMN</c>).</summary>
    [JsonPropertyName("recovery_codes")]
    public IReadOnlyList<string> RecoveryCodes { get; init; } = [];
}

/// <summary>Result of removing a two-factor method.</summary>
public sealed record MfaMethodRemoval
{
    /// <summary>Whether the user still has two-factor authentication enabled.</summary>
    [JsonPropertyName("is_mfa_enabled")]
    public bool IsMfaEnabled { get; init; }
}

/// <summary>Body for <c>POST /users/self/mfa/totp</c> — start an authenticator enrollment.</summary>
public sealed class StartTotpEnrollmentRequest
{
    /// <summary>Label for the device (e.g. <c>My phone</c>), or <see langword="null"/> to omit.</summary>
    public string? Label { get; set; }
}

/// <summary>Body for <c>PUT /users/self/mfa/totp/confirm</c> — activate an authenticator enrollment.</summary>
public sealed class ConfirmTotpEnrollmentRequest
{
    /// <summary>The <see cref="TotpEnrollment.Id"/> being confirmed.</summary>
    public required string Id { get; set; }

    /// <summary>A live code from the new device.</summary>
    public required string Code { get; set; }

    /// <summary>Current password; required only when replacing an existing confirmed method (alternative to <see cref="ReauthCode"/>).</summary>
    public string? Password { get; set; }

    /// <summary>A live code from the current device or a recovery code; required only when replacing an existing confirmed method (alternative to <see cref="Password"/>).</summary>
    public string? ReauthCode { get; set; }
}

/// <summary>Re-authentication proof for regenerating recovery codes or removing a two-factor method: the current password, a live authenticator code, or a recovery code (which is consumed).</summary>
public sealed class MfaReauthenticationRequest
{
    /// <summary>The user's current password.</summary>
    public string? Password { get; set; }

    /// <summary>A live 6-digit authenticator code, or one of the existing recovery codes.</summary>
    public string? Code { get; set; }
}
