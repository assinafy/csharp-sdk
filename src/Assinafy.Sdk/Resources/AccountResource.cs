using System.Net.Http.Headers;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>
/// Workspace account management: list the caller's accounts, create/read/update/delete
/// an account, read its branding theme, and manage its logo image. The account-scoped
/// methods fall back to the client's default account ID when none is passed.
/// </summary>
public sealed class AccountResource : BaseResource
{
    internal AccountResource(HttpClient http, string? defaultAccountId = null, Action<HttpRequestMessage>? authenticate = null)
        : base(http, defaultAccountId, authenticate) { }

    /// <summary><c>GET /accounts</c> — list the workspace accounts the authenticated user belongs to.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await CallListBodyAsync<Account>(
            "accounts",
            HttpMethod.Get,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary><c>POST /accounts</c> — create a new workspace account owned by the authenticated user.</summary>
    /// <param name="request">New account details; <c>Name</c> is required.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<Account> CreateAsync(
        CreateAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        return CallAsync<Account>(
            "accounts",
            HttpMethod.Post,
            request,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /accounts/{account_id}</c> — retrieve a workspace account the user belongs to.</summary>
    /// <param name="accountId">Account to retrieve; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<Account> GetAsync(
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        return CallAsync<Account>(
            $"accounts/{id}",
            HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>PUT /accounts/{account_id}</c> — update a workspace account's profile. Only non-null request properties are sent.</summary>
    /// <param name="request">Fields to update; only non-null properties are sent, leaving the rest unchanged.</param>
    /// <param name="accountId">Account to update; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<Account> UpdateAsync(
        UpdateAccountRequest request,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = AccountId(accountId);
        return CallAsync<Account>(
            $"accounts/{id}",
            HttpMethod.Put,
            request,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// <c>DELETE /accounts/{account_id}</c> — delete a workspace account. When <paramref name="force"/>
    /// is <see langword="false"/> (default) the API returns <c>400</c> with a <c>restrictions</c> list
    /// if the workspace has an active paid subscription; pass <see langword="true"/> to cancel any
    /// active subscription automatically and delete immediately.
    /// </summary>
    /// <param name="force">When <see langword="true"/>, cancels any active paid subscription and deletes immediately; when <see langword="false"/> (default) the delete is refused with <c>400</c> if a paid subscription is active.</param>
    /// <param name="accountId">Account to delete; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task DeleteAsync(
        bool force = false,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        return CallVoidAsync(
            $"accounts/{id}",
            HttpMethod.Delete,
            force ? new Dictionary<string, object?> { ["force"] = true } : null,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /accounts/{account_id}/theme</c> — retrieve the account's branding theme (name, colors, and logo URL).</summary>
    /// <param name="accountId">Account whose theme to retrieve; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<AccountTheme> GetThemeAsync(
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        return CallAsync<AccountTheme>(
            $"accounts/{id}/theme",
            HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /accounts/{account_id}/logo</c> — download the account logo image binary. Throws <c>404</c> when no logo is set.</summary>
    /// <param name="accountId">Account whose logo to download; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw logo image bytes.</returns>
    public Task<byte[]> DownloadLogoAsync(
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        return CallBinaryAsync($"accounts/{id}/logo", HttpMethod.Get, cancellationToken);
    }

    /// <summary><c>POST /accounts/{account_id}/logo</c> — upload or replace the account logo image (multipart <c>file</c> field).</summary>
    /// <param name="imageStream">The logo image content to upload.</param>
    /// <param name="fileName">File name for the uploaded logo.</param>
    /// <param name="contentType">MIME type of the image (default <c>image/png</c>).</param>
    /// <param name="accountId">Account whose logo to set; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task UploadLogoAsync(
        Stream imageStream,
        string fileName,
        string contentType = "image/png",
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        var id = AccountId(accountId);

        var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(imageStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);

        return CallContentVoidAsync(
            $"accounts/{id}/logo",
            HttpMethod.Post,
            content,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>DELETE /accounts/{account_id}/logo</c> — remove the account logo image.</summary>
    /// <param name="accountId">Account whose logo to remove; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task DeleteLogoAsync(
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        return CallVoidAsync($"accounts/{id}/logo", HttpMethod.Delete, cancellationToken: cancellationToken);
    }
}
