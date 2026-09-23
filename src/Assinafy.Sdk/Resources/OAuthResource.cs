using System.Security.Cryptography;
using System.Text;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>
/// OAuth 2.1 authorization-code flow with mandatory PKCE, for applications that act inside
/// <em>other people's</em> workspaces with those people's permission. Automating your own
/// workspace needs none of this — use an API key.
/// </summary>
/// <remarks>
/// <para>The flow spans two hosts on purpose: the browser-facing authorization page belongs to the
/// authorization server (<c>https://auth.assinafy.com.br</c>), while every call your code makes —
/// the token exchange included — belongs to this API.</para>
/// <para>The end-to-end shape is: <see cref="CreatePkcePair"/> and <see cref="CreateState"/> per
/// attempt, kept in the user's session; <see cref="BuildAuthorizationUrl"/> to send the browser
/// away; on return, compare <c>state</c> and <c>iss</c> before anything else; then
/// <see cref="ExchangeCodeAsync"/> from your server. A token is bound to the single workspace the
/// user picked, so read that workspace's ID from <c>Accounts.ListAsync()</c> and store it beside
/// the tokens.</para>
/// <para>These endpoints follow the RFC body contracts rather than this API's
/// <c>{ status, message, data }</c> envelope, and they authenticate with the application's own
/// credentials passed per call, never with the client's configured API key.</para>
/// </remarks>
public sealed class OAuthResource : BaseResource
{
    /// <summary>The production authorization endpoint the user's browser is sent to.</summary>
    public const string DefaultAuthorizationEndpoint = "https://auth.assinafy.com.br/oauth/authorize";

    /// <summary>The production issuer, which a redirect's <c>iss</c> parameter must equal.</summary>
    public const string DefaultIssuer = "https://auth.assinafy.com.br";

    /// <summary>The RFC 8707 resource indicator identifying this API as the token's audience.</summary>
    public const string DefaultResource = "https://api.assinafy.com.br";

    /// <summary>Characters RFC 7636 allows in a <c>code_verifier</c>.</summary>
    private const string VerifierUnreserved = "-._~";

    internal OAuthResource(HttpClient http, Action<HttpRequestMessage>? authenticate = null)
        : base(http, authenticate: authenticate) { }

    /// <summary>
    /// Create a fresh PKCE verifier/challenge pair for one connection attempt, using 256 bits from
    /// a cryptographic RNG.
    /// </summary>
    /// <remarks>
    /// Never reuse a pair across attempts. Send <see cref="OAuthPkcePair.CodeChallenge"/> to the
    /// authorization endpoint and keep <see cref="OAuthPkcePair.CodeVerifier"/> server-side until
    /// the exchange.
    /// </remarks>
    /// <returns>A verifier and its S256 challenge.</returns>
    public static OAuthPkcePair CreatePkcePair()
    {
        var verifier = Base64Url(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return new OAuthPkcePair { CodeVerifier = verifier, CodeChallenge = challenge };
    }

    /// <summary>
    /// Create an opaque random <c>state</c> value (128 bits) for one connection attempt. Store it
    /// in the user's session and reject any redirect whose <c>state</c> differs.
    /// </summary>
    /// <returns>A URL-safe random string.</returns>
    public static string CreateState() => Base64Url(RandomNumberGenerator.GetBytes(16));

    /// <summary>
    /// Build the authorization URL to send the user's browser to, as a full page navigation
    /// (a link or redirect) rather than an AJAX call.
    /// </summary>
    /// <remarks>
    /// If the <c>client_id</c> or <c>redirect_uri</c> is wrong the user is not redirected back:
    /// the authorization server shows its own error page, because redirecting to an unverified
    /// address would be unsafe. A user stuck on that page almost always means one of those two
    /// values is wrong.
    /// </remarks>
    /// <param name="request">Client, redirect, scope, state, and PKCE challenge for this attempt.</param>
    /// <returns>The absolute URL to navigate the browser to.</returns>
    public static Uri BuildAuthorizationUrl(OAuthAuthorizationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ClientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RedirectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.State);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.CodeChallenge);
        if (request.Scopes is not { Count: > 0 })
            throw new ValidationException("At least one scope is required.");
        if (request.Scopes.Any(string.IsNullOrWhiteSpace))
            throw new ValidationException("Scopes must not contain blank entries.");

        var endpoint = request.AuthorizationEndpoint ?? DefaultAuthorizationEndpoint;
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri) ||
            endpointUri.Scheme != Uri.UriSchemeHttps)
            throw new ValidationException("AuthorizationEndpoint must be an absolute HTTPS URL.");

        var parameters = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["response_type"] = "code",
            ["client_id"] = request.ClientId,
            ["redirect_uri"] = request.RedirectUri,
            ["scope"] = string.Join(' ', request.Scopes),
            ["state"] = request.State,
            ["code_challenge"] = request.CodeChallenge,
            ["code_challenge_method"] = "S256",
            ["resource"] = request.Resource ?? DefaultResource,
            ["nonce"] = request.Nonce,
        };

        return new Uri(AppendQueryString(endpointUri.GetLeftPart(UriPartial.Path), parameters));
    }

    /// <summary>
    /// <c>POST /oauth/token</c> with <c>grant_type=authorization_code</c> — exchange the one-time
    /// code from the redirect for an access token, and a refresh token when
    /// <see cref="OAuthScopes.OfflineAccess"/> was approved.
    /// </summary>
    /// <remarks>
    /// Call this from your server: the code is single-use and expires 60 seconds after approval.
    /// Verify the redirect's <c>state</c> and <c>iss</c> before calling.
    /// </remarks>
    /// <param name="request">Code, redirect URI, PKCE verifier, and application credentials.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The granted tokens and the scopes actually approved.</returns>
    /// <exception cref="OAuthException">
    /// <c>invalid_grant</c> when the code is spent, expired, or paired with the wrong verifier or
    /// redirect URI; <c>invalid_client</c> when the application credentials are refused.
    /// </exception>
    public Task<OAuthTokenResult> ExchangeCodeAsync(
        OAuthCodeExchangeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Code);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RedirectUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ClientId);
        ValidateCodeVerifier(request.CodeVerifier);

        return PostTokenAsync(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "authorization_code",
                ["code"] = request.Code,
                ["redirect_uri"] = request.RedirectUri,
                ["code_verifier"] = request.CodeVerifier,
                ["client_id"] = request.ClientId,
            },
            request.ClientSecret,
            request.Resource ?? DefaultResource,
            cancellationToken);
    }

    /// <summary>
    /// <c>POST /oauth/token</c> with <c>grant_type=refresh_token</c> — renew an expired access
    /// token without involving the user.
    /// </summary>
    /// <remarks>
    /// Refresh tokens rotate: persist the returned <see cref="OAuthTokenResult.RefreshToken"/>
    /// before doing anything else, never retry blindly with the old one after a timeout, and
    /// refresh one at a time per connection — replaying a retired token disconnects the user
    /// entirely. A connection lasts 30 days from approval and refreshing does not extend it.
    /// </remarks>
    /// <param name="request">The current refresh token and the application credentials.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A new access token and a new refresh token.</returns>
    /// <exception cref="OAuthException">
    /// <c>invalid_grant</c> when the refresh token is spent or expired, or when the user
    /// reconnected with different permissions — send them through the flow again.
    /// </exception>
    public Task<OAuthTokenResult> RefreshTokenAsync(
        OAuthRefreshRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RefreshToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ClientId);

        return PostTokenAsync(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = request.RefreshToken,
                ["client_id"] = request.ClientId,
            },
            request.ClientSecret,
            request.Resource,
            cancellationToken);
    }

    /// <summary>
    /// <c>POST /oauth/revoke</c> — revoke an access or refresh token when a user disconnects.
    /// Revoking the refresh token ends the whole connection.
    /// </summary>
    /// <remarks>
    /// Every token outcome answers <c>200</c> — unknown, already revoked, and malformed tokens
    /// alike — so the endpoint cannot be used to probe whether a token exists. Only failed client
    /// authentication fails, with <c>invalid_client</c>.
    /// </remarks>
    /// <param name="request">The token to revoke and the application credentials.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes once the revocation is acknowledged.</returns>
    /// <exception cref="OAuthException"><c>invalid_client</c> when the application credentials are refused.</exception>
    public Task RevokeAsync(
        OAuthRevokeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Token);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ClientId);

        var form = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["token"] = request.Token,
            ["client_id"] = request.ClientId,
        };
        if (!string.IsNullOrWhiteSpace(request.ClientSecret))
            form["client_secret"] = request.ClientSecret;
        if (!string.IsNullOrWhiteSpace(request.TokenTypeHint))
            form["token_type_hint"] = request.TokenTypeHint;

        return CallUnwrappedAsync<object>(
            () => FormRequest("oauth/revoke", form),
            cancellationToken,
            authenticate: false,
            requireBody: false);
    }

    /// <summary>
    /// <c>GET /oauth/userinfo</c> — the OpenID Connect claims about the user who authorized the
    /// access token this client is configured with.
    /// </summary>
    /// <remarks>
    /// Requires the <c>openid</c> scope; <c>name</c> additionally requires <c>profile</c> and
    /// <c>email</c> requires <c>email</c>. Configure the client's
    /// <see cref="AssinafyClientOptions.Token"/> with the OAuth access token before calling.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The user's claims.</returns>
    /// <exception cref="OAuthException"><c>insufficient_scope</c> when the token lacks <c>openid</c>.</exception>
    public async Task<OAuthUserInfo> GetUserInfoAsync(CancellationToken cancellationToken = default)
    {
        return await CallUnwrappedAsync<OAuthUserInfo>(
                   () => new HttpRequestMessage(HttpMethod.Get, "oauth/userinfo"),
                   cancellationToken,
                   authenticate: true,
                   requireBody: true).ConfigureAwait(false)
               ?? throw new SerializationException("The userinfo endpoint returned no claims.");
    }

    /// <summary>
    /// <c>GET /.well-known/oauth-protected-resource</c> — RFC 9728 metadata naming the
    /// authorization servers that issue tokens for this API and the scopes it accepts.
    /// </summary>
    /// <remarks>
    /// Served at the API host root, outside the <c>/v1</c> base path, and unauthenticated. Use
    /// <see cref="OAuthProtectedResourceMetadata.AuthorizationServers"/> to discover the
    /// authorization server rather than hard-coding its URLs.
    /// </remarks>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The protected-resource metadata.</returns>
    public async Task<OAuthProtectedResourceMetadata> GetProtectedResourceMetadataAsync(
        CancellationToken cancellationToken = default)
    {
        return await CallUnwrappedAsync<OAuthProtectedResourceMetadata>(
                   () => new HttpRequestMessage(HttpMethod.Get, RootUri(".well-known/oauth-protected-resource")),
                   cancellationToken,
                   authenticate: false,
                   requireBody: true).ConfigureAwait(false)
               ?? throw new SerializationException("The protected-resource metadata response was empty.");
    }

    private async Task<OAuthTokenResult> PostTokenAsync(
        Dictionary<string, string> form,
        string? clientSecret,
        string? resource,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(clientSecret))
            form["client_secret"] = clientSecret;
        if (!string.IsNullOrWhiteSpace(resource))
            form["resource"] = resource;

        var result = await CallUnwrappedAsync<OAuthTokenResult>(
            () => FormRequest("oauth/token", form),
            cancellationToken,
            authenticate: false,
            requireBody: true).ConfigureAwait(false);

        if (result is null || string.IsNullOrWhiteSpace(result.AccessToken))
            throw new SerializationException("The token endpoint returned no access token.");

        return result;
    }

    /// <summary>
    /// Build the <c>application/x-www-form-urlencoded</c> request the OAuth endpoints expect, as
    /// mandated by RFC 6749 §4.1.3 and used by every standard OAuth client.
    /// </summary>
    private static HttpRequestMessage FormRequest(string path, Dictionary<string, string> form) =>
        new(HttpMethod.Post, path) { Content = new FormUrlEncodedContent(form) };

    /// <summary>
    /// Reject a <c>code_verifier</c> outside the RFC 7636 grammar before spending the one-time
    /// authorization code on it — the server answers <c>invalid_grant</c>, which is
    /// indistinguishable from a genuinely expired code.
    /// </summary>
    private static void ValidateCodeVerifier(string? codeVerifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codeVerifier);

        if (codeVerifier.Length is < 43 or > 128)
            throw new ValidationException(
                "CodeVerifier must be 43 to 128 characters long (RFC 7636).",
                new Dictionary<string, object?> { ["length"] = codeVerifier.Length });

        if (!codeVerifier.All(c => char.IsAsciiLetterOrDigit(c) || VerifierUnreserved.Contains(c)))
            throw new ValidationException(
                "CodeVerifier must contain only the characters A-Z a-z 0-9 - . _ ~ (RFC 7636).");
    }

    /// <summary>Base64url without padding, per RFC 4648 §5 — the encoding PKCE and OAuth require.</summary>
    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
