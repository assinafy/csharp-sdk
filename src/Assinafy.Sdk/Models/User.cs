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
