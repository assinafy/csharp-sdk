using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>An exact origin allowed to host the workspace's embedded signing page.</summary>
public sealed record EmbedOrigin
{
    /// <summary>Origin registration identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Canonical scheme, host, and optional port, without a trailing slash.</summary>
    [JsonPropertyName("origin")]
    public string Origin { get; init; } = string.Empty;

    /// <summary>Registration time as an ISO-8601 date-time.</summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;
}

/// <summary>A single-use embedded signing URL, valid for five minutes.</summary>
public sealed record EmbedSession
{
    /// <summary>Bearer URL to open once in an iframe on a registered origin. Never log it.</summary>
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    /// <summary>Session expiry as an ISO-8601 date-time.</summary>
    [JsonPropertyName("expires_at")]
    public string ExpiresAt { get; init; } = string.Empty;

    /// <summary>Returns a representation that does not expose the bearer URL.</summary>
    public override string ToString() => $"{nameof(EmbedSession)} {{ <redacted> }}";
}

/// <summary>The signing session returned to Assinafy's embed page after a one-time token exchange.</summary>
public sealed record EmbedSessionExchange
{
    /// <summary>Bearer signer access code for the embed page only. Never log or persist it in browser storage.</summary>
    [JsonPropertyName("signer_access_code")]
    public string SignerAccessCode { get; init; } = string.Empty;

    /// <summary>The authorized signer identifier.</summary>
    [JsonPropertyName("signer_id")]
    public string SignerId { get; init; } = string.Empty;

    /// <summary>The authorized document identifier.</summary>
    [JsonPropertyName("document_id")]
    public string DocumentId { get; init; } = string.Empty;

    /// <summary>The signature request's verification method.</summary>
    [JsonPropertyName("verification_method")]
    public string VerificationMethod { get; init; } = string.Empty;

    /// <summary>The origins allowed to host the signing iframe.</summary>
    [JsonPropertyName("embed_origins")]
    public IReadOnlyList<string> EmbedOrigins { get; init; } = [];

    /// <summary>Returns a representation that does not expose the signer credential.</summary>
    public override string ToString() => $"{nameof(EmbedSessionExchange)} {{ <redacted> }}";
}
