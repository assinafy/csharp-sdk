using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>
/// The permissions an OAuth application may request. Ask for the minimum: the user approves the
/// whole set or none of it, and every extra entry is another line they read before deciding.
/// </summary>
/// <remarks>
/// Billing and subscriptions, workspace membership, credential management, and administration are
/// never reachable with an OAuth token, whatever scopes it carries.
/// </remarks>
public static class OAuthScopes
{
    /// <summary>Read documents, their pages, tags, signers, assignments, and activity.</summary>
    public const string DocumentsRead = "documents:read";

    /// <summary>Create, update, and delete documents and manage their signers and assignments. Sending for signature spends the workspace's notification credits.</summary>
    public const string DocumentsWrite = "documents:write";

    /// <summary>Read reusable document templates, their pages, roles, fields, and tags.</summary>
    public const string TemplatesRead = "templates:read";

    /// <summary>Create, update, and delete templates, their pages, roles, fields, and tags.</summary>
    public const string TemplatesWrite = "templates:write";

    /// <summary>Read the workspace's profile, theme, and logo.</summary>
    public const string AccountRead = "account:read";

    /// <summary>Configure and deactivate the workspace webhook subscription.</summary>
    public const string WebhooksWrite = "webhooks:write";

    /// <summary>Receive a signed OpenID Connect <c>id_token</c> identifying the user who approved.</summary>
    public const string OpenId = "openid";

    /// <summary>Read the user's name, returned as the <c>name</c> claim.</summary>
    public const string Profile = "profile";

    /// <summary>Read the user's email address and whether it is verified.</summary>
    public const string Email = "email";

    /// <summary>
    /// Receive a refresh token, so the application keeps working while the user is away. A
    /// request-time signal rather than a permission: it never appears in a granted access
    /// token's <see cref="OAuthTokenResult.Scope"/>.
    /// </summary>
    public const string OfflineAccess = "offline_access";
}

/// <summary>
/// A PKCE (RFC 7636) verifier and its S256 challenge. Create one per connection attempt with
/// <see cref="Resources.OAuthResource.CreatePkcePair"/>: send
/// <see cref="CodeChallenge"/> to the authorization endpoint and keep
/// <see cref="CodeVerifier"/> in the user's session for the token exchange.
/// </summary>
public sealed record OAuthPkcePair
{
    /// <summary>The high-entropy secret, 43 to 128 characters from <c>A-Z a-z 0-9 - . _ ~</c>. Never send it to the authorization endpoint.</summary>
    public required string CodeVerifier { get; init; }

    /// <summary>The base64url-encoded SHA-256 of <see cref="CodeVerifier"/>, sent as <c>code_challenge</c>.</summary>
    public required string CodeChallenge { get; init; }

    /// <summary>The challenge method, always <c>S256</c>: the authorization server accepts no other.</summary>
    public string CodeChallengeMethod => "S256";
}

/// <summary>
/// The values that make up the authorization URL the user's browser is sent to, built by
/// <see cref="Resources.OAuthResource.BuildAuthorizationUrl"/>.
/// </summary>
public sealed class OAuthAuthorizationRequest
{
    /// <summary>The application's <c>client_id</c>, issued when the application is registered.</summary>
    public required string ClientId { get; set; }

    /// <summary>
    /// Where the user returns after approving. Must be one of the application's registered
    /// redirect URIs, matched character for character — <c>…/callback</c> and <c>…/callback/</c>
    /// are different URIs.
    /// </summary>
    public required string RedirectUri { get; set; }

    /// <summary>The permissions being requested — see <see cref="OAuthScopes"/>. Sent space-separated.</summary>
    public required IReadOnlyList<string> Scopes { get; set; }

    /// <summary>Opaque per-attempt random value echoed back on the redirect; the application's CSRF protection. Generate it with <see cref="Resources.OAuthResource.CreateState"/>.</summary>
    public required string State { get; set; }

    /// <summary>The <see cref="OAuthPkcePair.CodeChallenge"/> for this attempt. PKCE is required for every application, confidential ones included.</summary>
    public required string CodeChallenge { get; set; }

    /// <summary>
    /// RFC 8707 resource indicator naming the API the token is for. Defaults to
    /// <see cref="Resources.OAuthResource.DefaultResource"/>; the same value must be sent to the
    /// token endpoint, or the exchange fails with <c>invalid_target</c>.
    /// </summary>
    public string? Resource { get; set; }

    /// <summary>Optional OpenID Connect nonce, echoed in the <c>id_token</c> when the <c>openid</c> scope is requested.</summary>
    public string? Nonce { get; set; }

    /// <summary>
    /// The authorization server's authorize endpoint. Defaults to
    /// <see cref="Resources.OAuthResource.DefaultAuthorizationEndpoint"/>. Override it for a
    /// non-production authorization server, taking the value from that server's
    /// <c>/.well-known/oauth-authorization-server</c> metadata.
    /// </summary>
    public string? AuthorizationEndpoint { get; set; }
}

/// <summary>
/// A successful token response from <c>POST /oauth/token</c> (RFC 6749 §5.1). Returned as a flat
/// JSON object rather than the API's <c>{ status, message, data }</c> envelope, so that standard
/// OAuth client libraries can read it unchanged.
/// </summary>
public sealed record OAuthTokenResult
{
    /// <summary>The access token to send as <c>Authorization: Bearer</c>. Valid for one hour, and refused if sent as an API key or in the query string.</summary>
    [JsonPropertyName("access_token")]
    public string AccessToken { get; init; } = string.Empty;

    /// <summary>The token type, always <c>Bearer</c>.</summary>
    [JsonPropertyName("token_type")]
    public string TokenType { get; init; } = string.Empty;

    /// <summary>Lifetime of <see cref="AccessToken"/> in seconds, typically 3600.</summary>
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; init; }

    /// <summary>
    /// The refresh token, present only when <see cref="OAuthScopes.OfflineAccess"/> was requested
    /// and approved. Each refresh returns a new one and retires the old one — persist it before
    /// doing anything else with the response.
    /// </summary>
    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; init; }

    /// <summary>
    /// The scopes actually granted, space-separated. Read it rather than assuming the request was
    /// granted in full. <c>offline_access</c> never appears here even when it was requested.
    /// </summary>
    [JsonPropertyName("scope")]
    public string? Scope { get; init; }

    /// <summary>The signed OpenID Connect ID token (RS256), present only when the <c>openid</c> scope was granted.</summary>
    [JsonPropertyName("id_token")]
    public string? IdToken { get; init; }

    /// <summary>The granted scopes split into individual entries, empty when the response carried none.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> GrantedScopes =>
        string.IsNullOrWhiteSpace(Scope)
            ? []
            : Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Whether the granted scopes include <paramref name="scope"/>.</summary>
    /// <param name="scope">The scope to look for, for example <see cref="OAuthScopes.DocumentsWrite"/>.</param>
    /// <returns><see langword="true"/> when the token carries the scope.</returns>
    public bool HasScope(string scope) =>
        GrantedScopes.Contains(scope, StringComparer.Ordinal);
}

/// <summary>Body for <c>POST /oauth/token</c> with <c>grant_type=authorization_code</c> — exchange the one-time code for tokens.</summary>
/// <remarks>The code is single-use and expires 60 seconds after the user approves.</remarks>
public sealed class OAuthCodeExchangeRequest
{
    /// <summary>The one-time authorization code from the redirect's <c>code</c> parameter.</summary>
    public required string Code { get; set; }

    /// <summary>The same redirect URI that was sent to the authorization endpoint. A mismatch fails with <c>invalid_grant</c>.</summary>
    public required string RedirectUri { get; set; }

    /// <summary>The <see cref="OAuthPkcePair.CodeVerifier"/> kept in the user's session for this attempt.</summary>
    public required string CodeVerifier { get; set; }

    /// <summary>The application's <c>client_id</c>.</summary>
    public required string ClientId { get; set; }

    /// <summary>The application's secret. Confidential applications only; public applications authenticate with PKCE alone and are never issued one.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>RFC 8707 resource indicator. Must equal the value sent to the authorization endpoint, or the exchange fails with <c>invalid_target</c>. Defaults to <see cref="Resources.OAuthResource.DefaultResource"/>.</summary>
    public string? Resource { get; set; }
}

/// <summary>Body for <c>POST /oauth/token</c> with <c>grant_type=refresh_token</c> — renew an access token without the user.</summary>
/// <remarks>
/// Refreshing rotates the token: the response carries a new refresh token and the old one stops
/// working. Replaying a retired refresh token cannot be told apart from a stolen one being
/// replayed, so it ends the entire connection and the user must reconnect. Persist the new token
/// before anything else, treat a timeout as "it may have succeeded" by re-reading the stored token
/// rather than retrying with the old one, and refresh one at a time per connection.
/// </remarks>
public sealed class OAuthRefreshRequest
{
    /// <summary>The current refresh token.</summary>
    public required string RefreshToken { get; set; }

    /// <summary>The application's <c>client_id</c>.</summary>
    public required string ClientId { get; set; }

    /// <summary>The application's secret, for confidential applications.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>RFC 8707 resource indicator, when one was used to obtain the token.</summary>
    public string? Resource { get; set; }
}

/// <summary>Body for <c>POST /oauth/revoke</c> — disconnect a user by revoking their token.</summary>
public sealed class OAuthRevokeRequest
{
    /// <summary>The access or refresh token to revoke. Revoking a refresh token ends the whole connection.</summary>
    public required string Token { get; set; }

    /// <summary>The application's <c>client_id</c>.</summary>
    public required string ClientId { get; set; }

    /// <summary>The application's secret, for confidential applications.</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Optional hint, <c>access_token</c> or <c>refresh_token</c>, letting the server look in the right place first.</summary>
    public string? TokenTypeHint { get; set; }
}

/// <summary>
/// OpenID Connect claims about the user who authorized the token, from <c>GET /oauth/userinfo</c>.
/// Returned as a flat JSON object of claims rather than the API's usual envelope.
/// </summary>
public sealed record OAuthUserInfo
{
    /// <summary>Stable identifier for the user who approved the application.</summary>
    [JsonPropertyName("sub")]
    public string Sub { get; init; } = string.Empty;

    /// <summary>The user's name. Present only when the <c>profile</c> scope was granted.</summary>
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    /// <summary>The user's email address. Present only when the <c>email</c> scope was granted.</summary>
    [JsonPropertyName("email")]
    public string? Email { get; init; }

    /// <summary>Whether the email address is verified. Present only when the <c>email</c> scope was granted.</summary>
    [JsonPropertyName("email_verified")]
    public bool? EmailVerified { get; init; }
}

/// <summary>
/// RFC 9728 protected-resource metadata from <c>GET /.well-known/oauth-protected-resource</c>:
/// which authorization servers issue tokens for this API and which scopes it accepts.
/// </summary>
/// <remarks>
/// Served as the bare metadata object, never wrapped in the API's response envelope. Start an
/// integration by reading <see cref="AuthorizationServers"/> and fetching that host's own
/// <c>/.well-known/oauth-authorization-server</c> document for the browser-facing endpoints.
/// </remarks>
public sealed record OAuthProtectedResourceMetadata
{
    /// <summary>Canonical identifier of this API, and the value to send as the <c>resource</c> indicator.</summary>
    [JsonPropertyName("resource")]
    public string Resource { get; init; } = string.Empty;

    /// <summary>The authorization servers that issue tokens for this API.</summary>
    [JsonPropertyName("authorization_servers")]
    public IReadOnlyList<string> AuthorizationServers { get; init; } = [];

    /// <summary>The scopes this API accepts. Deliberately excludes <c>offline_access</c>, which is a client concern rather than a resource permission.</summary>
    [JsonPropertyName("scopes_supported")]
    public IReadOnlyList<string> ScopesSupported { get; init; } = [];

    /// <summary>How a token may be presented; this API accepts <c>header</c> only.</summary>
    [JsonPropertyName("bearer_methods_supported")]
    public IReadOnlyList<string> BearerMethodsSupported { get; init; } = [];
}
