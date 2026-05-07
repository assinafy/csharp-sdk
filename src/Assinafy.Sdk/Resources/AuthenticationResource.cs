using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Authentication and user API-key endpoints.</summary>
public sealed class AuthenticationResource : BaseResource
{
    internal AuthenticationResource(HttpClient http) : base(http) { }

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

    public Task<ApiKeyResult> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        return CallAsync<ApiKeyResult>(
            "users/api-keys",
            HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    public Task DeleteApiKeyAsync(CancellationToken cancellationToken = default)
    {
        return CallVoidAsync(
            "users/api-keys",
            HttpMethod.Delete,
            cancellationToken: cancellationToken);
    }

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
            cancellationToken: cancellationToken);
    }
}
