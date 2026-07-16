using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>A workspace account (organization) the authenticated user belongs to, as returned in the login and social-login response.</summary>
public sealed record UserAccount
{
    /// <summary>Unique account identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Account display name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>The authenticated user's roles within this account (e.g. <c>owner</c>).</summary>
    [JsonPropertyName("roles")]
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>Whether the authenticated user is allowed to delete this account.</summary>
    [JsonPropertyName("is_delete_allowed")]
    public bool IsDeleteAllowed { get; init; }

    /// <summary>Account creation timestamp (ISO-8601 date-time).</summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;
}

/// <summary>The authenticated user returned by the login and social-login endpoints.</summary>
public sealed record UserProfile
{
    /// <summary>Unique user identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>User's full name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>User's email address.</summary>
    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;

    /// <summary>User's telephone number, or <see langword="null"/> when not set.</summary>
    [JsonPropertyName("telephone")]
    public string? Telephone { get; init; }

    /// <summary>User's government ID (e.g. CPF/CNPJ), or <see langword="null"/> when not set.</summary>
    [JsonPropertyName("government_id")]
    public string? GovernmentId { get; init; }

    /// <summary>Whether the user's email address has been verified.</summary>
    [JsonPropertyName("is_email_verified")]
    public bool IsEmailVerified { get; init; }

    /// <summary>Whether the user has accepted the terms of use.</summary>
    [JsonPropertyName("has_accepted_terms")]
    public bool HasAcceptedTerms { get; init; }

    /// <summary>Whether the user has a password set, or <see langword="null"/> when the endpoint does not report it.</summary>
    [JsonPropertyName("is_password_set")]
    public bool? IsPasswordSet { get; init; }

    /// <summary>User creation timestamp (ISO-8601 date-time).</summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;

    /// <summary>Scheduled account-deletion timestamp (ISO-8601 date-time), or <see langword="null"/> when deletion is not scheduled.</summary>
    [JsonPropertyName("to_be_deleted_at")]
    public string? ToBeDeletedAt { get; init; }
}

/// <summary>Result of a successful login or social-login: an access token plus the authenticated user and the accounts they belong to.</summary>
public sealed record AuthenticationResult
{
    /// <summary>JWT access token to send as the bearer credential on subsequent requests.</summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>The authenticated user, or <see langword="null"/> when not included in the response.</summary>
    [JsonPropertyName("user")]
    public UserProfile? User { get; init; }

    /// <summary>The accounts the authenticated user belongs to.</summary>
    [JsonPropertyName("accounts")]
    public IReadOnlyList<UserAccount> Accounts { get; init; } = [];
}

/// <summary>Result of an API-key operation: the full key on creation (shown only once), a masked value on retrieval, or <see langword="null"/> when no key exists.</summary>
public sealed record ApiKeyResult
{
    /// <summary>The API key value — full on creation, masked on retrieval, or <see langword="null"/> when no key has been generated.</summary>
    [JsonPropertyName("api_key")]
    public string? ApiKey { get; init; }
}

/// <summary>Result carrying the email address of the account affected by a password operation (change, reset request, or reset).</summary>
public sealed record EmailResult
{
    /// <summary>The email address of the affected account.</summary>
    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;
}

/// <summary>Body for <c>POST /login</c> — authenticate with email and password.</summary>
public sealed class LoginRequest
{
    /// <summary>Account email address.</summary>
    public required string Email { get; set; }

    /// <summary>Account password.</summary>
    public required string Password { get; set; }
}

/// <summary>Body for <c>POST /authentication/social-login</c> — exchange a social-provider token for an Assinafy access token.</summary>
public sealed class SocialLoginRequest
{
    /// <summary>Social provider name (currently only <c>google</c>).</summary>
    public required string Provider { get; set; }

    /// <summary>Access or ID token issued by the social provider.</summary>
    public required string Token { get; set; }

    /// <summary>Whether the user accepts the terms of use.</summary>
    public required bool HasAcceptedTerms { get; set; }
}

/// <summary>Body for <c>POST /users/api-keys</c> — generate a personal API key. Generating a new key deletes the previous one.</summary>
public sealed class CreateApiKeyRequest
{
    /// <summary>The user's current password, required to authorize key generation.</summary>
    public required string Password { get; set; }
}

/// <summary>Body for <c>POST /auth/link-social-login</c> — link a social provider to the authenticated user.</summary>
public sealed class LinkSocialLoginRequest
{
    /// <summary>Social provider name (e.g. <c>google</c>).</summary>
    public required string Provider { get; set; }

    /// <summary>The provider-issued token identifying the account to link.</summary>
    public required string Token { get; set; }
}

/// <summary>Body for <c>PUT /authentication/change-password</c> — change the authenticated user's password.</summary>
public sealed class ChangePasswordRequest
{
    /// <summary>The user's email address.</summary>
    public required string Email { get; set; }

    /// <summary>The user's current password.</summary>
    public required string Password { get; set; }

    /// <summary>The new password to set.</summary>
    public required string NewPassword { get; set; }
}

/// <summary>Body for <c>PUT /authentication/request-password-reset</c> — email the user a password-reset token.</summary>
public sealed class RequestPasswordResetRequest
{
    /// <summary>The email address to send reset instructions to.</summary>
    public required string Email { get; set; }
}

/// <summary>Body for <c>PUT /authentication/reset-password</c> — set a new password using the token emailed by <c>RequestPasswordResetAsync</c>.</summary>
public sealed class ResetPasswordRequest
{
    /// <summary>The user's email address.</summary>
    public required string Email { get; set; }

    /// <summary>The reset token delivered by <c>RequestPasswordResetAsync</c>. Required by the API and validated at call time.</summary>
    public string? Token { get; set; }

    /// <summary>The new password to set.</summary>
    public required string NewPassword { get; set; }
}
