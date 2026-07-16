using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>Well-known values for <see cref="Account.NotificationSenderType"/> and the account create/update requests.</summary>
public static class AccountNotificationSenderTypes
{
    /// <summary>Signers see the individual user as the notification sender.</summary>
    public const string User = "User";

    /// <summary>Signers see the workspace account (organization) as the notification sender.</summary>
    public const string Account = "Account";
}

/// <summary>
/// A workspace account (organization). Returned by the account endpoints
/// (<c>GET/POST /accounts</c>, <c>GET/PUT /accounts/{account_id}</c>). This is the
/// full workspace object; the lighter <see cref="UserAccount"/> is the membership
/// summary returned inside the login response.
/// </summary>
public sealed record Account
{
    /// <summary>Resource type discriminator; <c>account</c> for workspace accounts.</summary>
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    /// <summary>The account's unique identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>The workspace display name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Primary brand color as a six-character hex string without the leading <c>#</c>, or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("primary_color")]
    public string? PrimaryColor { get; init; }

    /// <summary>Secondary brand color as a six-character hex string without the leading <c>#</c>, or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("secondary_color")]
    public string? SecondaryColor { get; init; }

    /// <summary>Who signers see as the notification sender: <c>User</c> or <c>Account</c> (see <see cref="AccountNotificationSenderTypes"/>).</summary>
    [JsonPropertyName("notification_sender_type")]
    public string? NotificationSenderType { get; init; }

    /// <summary>The authenticated user's roles within this account (e.g. <c>owner</c>).</summary>
    [JsonPropertyName("roles")]
    public IReadOnlyList<string> Roles { get; init; } = [];

    /// <summary>Whether the current user is allowed to delete this account.</summary>
    [JsonPropertyName("is_delete_allowed")]
    public bool IsDeleteAllowed { get; init; }

    /// <summary>ISO-8601 date-time when the account was created.</summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;
}

/// <summary>
/// An account's branding theme, returned by <c>GET /accounts/{account_id}/theme</c>.
/// Unlike <see cref="Account"/>, this is a public-facing projection intended for
/// rendering a signer's branded experience.
/// </summary>
public sealed record AccountTheme
{
    /// <summary>The account's branding display name.</summary>
    [JsonPropertyName("account_name")]
    public string AccountName { get; init; } = string.Empty;

    /// <summary>Primary color as a six-character hex string without the leading <c>#</c>.</summary>
    [JsonPropertyName("primary_color")]
    public string? PrimaryColor { get; init; }

    /// <summary>Secondary color as a six-character hex string without the leading <c>#</c>, or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("secondary_color")]
    public string? SecondaryColor { get; init; }

    /// <summary>Absolute URL to the account logo image, or <see langword="null"/> when no logo is set.</summary>
    [JsonPropertyName("logo")]
    public string? Logo { get; init; }
}

/// <summary>Body for <c>POST /accounts</c> — create a workspace account owned by the authenticated user.</summary>
public sealed class CreateAccountRequest
{
    /// <summary>Workspace display name (required).</summary>
    public required string Name { get; set; }

    /// <summary>Optional notification sender type: <c>User</c> or <c>Account</c> (see <see cref="AccountNotificationSenderTypes"/>).</summary>
    public string? NotificationSenderType { get; set; }
}

/// <summary>
/// Body for <c>PUT /accounts/{account_id}</c>. Only non-null properties are sent,
/// so an unset property leaves the existing value unchanged.
/// </summary>
public sealed class UpdateAccountRequest
{
    /// <summary>New workspace display name, or <see langword="null"/> to leave unchanged.</summary>
    public string? Name { get; set; }

    /// <summary>New notification sender type (<c>User</c> or <c>Account</c>), or <see langword="null"/> to leave unchanged.</summary>
    public string? NotificationSenderType { get; set; }
}
