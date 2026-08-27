using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Authentication and user API-key endpoints.</summary>
public sealed class AuthenticationResource : BaseResource
{
    internal AuthenticationResource(HttpClient http, Action<HttpRequestMessage>? authenticate = null)
        : base(http, authenticate: authenticate) { }

    /// <summary><c>POST /login</c> — exchange email and password for an access token and account list.</summary>
    /// <param name="request">The user's email and password credentials.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The access token, authenticated user, and associated accounts.</returns>
    public Task<AuthenticationResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);

        return CallAsync<AuthenticationResult>(
            "login",
            HttpMethod.Post,
            request,
            cancellationToken: cancellationToken,
            authenticate: false);
    }

    /// <summary><c>POST /authentication/social-login</c> — exchange a third-party provider token (e.g. Google) for an Assinafy access token.</summary>
    /// <param name="request">The provider name and the provider-issued token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The access token, authenticated user, and associated accounts.</returns>
    public Task<AuthenticationResult> SocialLoginAsync(
        SocialLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Token);

        return CallAsync<AuthenticationResult>(
            "authentication/social-login",
            HttpMethod.Post,
            request,
            cancellationToken: cancellationToken,
            authenticate: false);
    }

    /// <summary>
    /// <c>POST /auth/link-social-login</c> — link a social-login provider (e.g. Google) to the currently
    /// authenticated user. The API accepts either a bearer token or an API key.
    /// </summary>
    /// <param name="request">The provider name and the provider-issued token to link.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the social-login provider has been linked.</returns>
    public Task LinkSocialLoginAsync(
        LinkSocialLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Token);

        return CallVoidAsync(
            "auth/link-social-login",
            HttpMethod.Post,
            request,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>POST /users/api-keys</c> — generate a personal API key. Replaces any previous key for the user.</summary>
    /// <param name="request">The user's password, required to authorize key generation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly generated API key. The full value is shown only once.</returns>
    public Task<ApiKeyResult> CreateApiKeyAsync(
        CreateApiKeyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);

        return CallAsync<ApiKeyResult>(
            "users/api-keys",
            HttpMethod.Post,
            request,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /users/api-keys</c> — fetch a masked representation of the user's current API key.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The masked API key, or a result whose <see cref="ApiKeyResult.ApiKey"/> is <see langword="null"/> when none exists.</returns>
    public Task<ApiKeyResult> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        return CallAsync<ApiKeyResult>(
            "users/api-keys",
            HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>DELETE /users/api-keys</c> — revoke the user's current API key.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the API key has been revoked.</returns>
    public Task DeleteApiKeyAsync(CancellationToken cancellationToken = default)
    {
        return CallVoidAsync(
            "users/api-keys",
            HttpMethod.Delete,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>PUT /authentication/change-password</c> — change the user's password while authenticated.</summary>
    /// <param name="request">The user's email, current password, and new password.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The email address of the account whose password was changed.</returns>
    public Task<EmailResult> ChangePasswordAsync(
        ChangePasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.NewPassword);

        return CallAsync<EmailResult>(
            "authentication/change-password",
            HttpMethod.Put,
            request,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>PUT /authentication/request-password-reset</c> — email the user a password reset token.</summary>
    /// <param name="request">The email address to send the reset token to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The email address to which reset instructions were sent.</returns>
    public Task<EmailResult> RequestPasswordResetAsync(
        RequestPasswordResetRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);

        return CallAsync<EmailResult>(
            "authentication/request-password-reset",
            HttpMethod.Put,
            request,
            cancellationToken: cancellationToken,
            authenticate: false);
    }

    /// <summary><c>PUT /authentication/reset-password</c> — set a new password, optionally using the reset token from <see cref="RequestPasswordResetAsync"/>.</summary>
    /// <param name="request">The email, new password, and optional reset token.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The email address of the account whose password was reset.</returns>
    public Task<EmailResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.NewPassword);

        return CallAsync<EmailResult>(
            "authentication/reset-password",
            HttpMethod.Put,
            request,
            cancellationToken: cancellationToken,
            authenticate: false);
    }
}
