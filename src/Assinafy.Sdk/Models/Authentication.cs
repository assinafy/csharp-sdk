using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

public sealed record UserAccount
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("roles")]
    public IReadOnlyList<string> Roles { get; init; } = [];

    [JsonPropertyName("is_delete_allowed")]
    public bool IsDeleteAllowed { get; init; }

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;
}

public sealed record UserProfile
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("telephone")]
    public string? Telephone { get; init; }

    [JsonPropertyName("government_id")]
    public string? GovernmentId { get; init; }

    [JsonPropertyName("is_email_verified")]
    public bool IsEmailVerified { get; init; }

    [JsonPropertyName("has_accepted_terms")]
    public bool HasAcceptedTerms { get; init; }

    [JsonPropertyName("is_password_set")]
    public bool? IsPasswordSet { get; init; }

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;

    [JsonPropertyName("to_be_deleted_at")]
    public string? ToBeDeletedAt { get; init; }
}

public sealed record AuthenticationResult
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    [JsonPropertyName("user")]
    public UserProfile? User { get; init; }

    [JsonPropertyName("accounts")]
    public IReadOnlyList<UserAccount> Accounts { get; init; } = [];
}

public sealed record ApiKeyResult
{
    [JsonPropertyName("api_key")]
    public string? ApiKey { get; init; }
}

public sealed record EmailResult
{
    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;
}

public sealed class LoginRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
}

public sealed class SocialLoginRequest
{
    public required string Provider { get; set; }
    public required string Token { get; set; }
    public required bool HasAcceptedTerms { get; set; }
}

public sealed class CreateApiKeyRequest
{
    public required string Password { get; set; }
}

public sealed class ChangePasswordRequest
{
    public required string Email { get; set; }
    public required string Password { get; set; }
    public required string NewPassword { get; set; }
}

public sealed class RequestPasswordResetRequest
{
    public required string Email { get; set; }
}

public sealed class ResetPasswordRequest
{
    public required string Email { get; set; }
    public string? Token { get; set; }
    public required string NewPassword { get; set; }
}
