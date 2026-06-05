using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Authentication and user API-key endpoints.</summary>
public sealed class AuthenticationResource : BaseResource
{
    internal AuthenticationResource(HttpClient http, Action<HttpRequestMessage>? authenticate = null)
        : base(http, authenticate: authenticate) { }

    /// <summary><c>POST /login</c> — exchange email and password for an access token and account list.</summary>
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
            cancellationToken: cancellationToken);
    }

    /// <summary><c>POST /authentication/social-login</c> — exchange a third-party provider token (e.g. Google) for an Assinafy access token.</summary>
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
            cancellationToken: cancellationToken);
    }

    /// <summary><c>POST /users/api-keys</c> — generate a personal API key. Replaces any previous key for the user.</summary>
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
    public Task<ApiKeyResult> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        return CallAsync<ApiKeyResult>(
            "users/api-keys",
            HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>DELETE /users/api-keys</c> — revoke the user's current API key.</summary>
    public Task DeleteApiKeyAsync(CancellationToken cancellationToken = default)
    {
        return CallVoidAsync(
            "users/api-keys",
            HttpMethod.Delete,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>PUT /authentication/change-password</c> — change the user's password while authenticated.</summary>
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
            cancellationToken: cancellationToken);
    }

    /// <summary><c>PUT /authentication/reset-password</c> — set a new password using the reset token from <see cref="RequestPasswordResetAsync"/>.</summary>
    public Task<EmailResult> ResetPasswordAsync(
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Token);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.NewPassword);

        return CallAsync<EmailResult>(
            "authentication/reset-password",
            HttpMethod.Put,
            request,
            cancellationToken: cancellationToken);
    }
}
