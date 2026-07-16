using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>
/// Workspace tag management and document tag attachment.
/// Tags are unique per workspace (case-insensitive) and can be attached to documents.
/// </summary>
public sealed class TagResource : BaseResource
{
    internal TagResource(HttpClient http, string? defaultAccountId = null, Action<HttpRequestMessage>? authenticate = null)
        : base(http, defaultAccountId, authenticate) { }

    /// <summary><c>GET /accounts/{account_id}/tags</c> — list workspace tags ordered alphabetically, optionally filtered by a case-insensitive <paramref name="search"/> substring.</summary>
    /// <param name="search">Optional case-insensitive substring to filter tag names.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<Tag>> ListAsync(
        string? search = null,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var path = AppendQueryString(
            $"accounts/{id}/tags",
            string.IsNullOrWhiteSpace(search)
                ? null
                : new Dictionary<string, string?> { ["search"] = search });

        return await CallListBodyAsync<Tag>(
            path,
            HttpMethod.Get,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary><c>POST /accounts/{account_id}/tags</c> — create a tag. The API returns <c>409 Conflict</c> if the name already exists (case-insensitive).</summary>
    /// <param name="request">Tag name and optional hex color to create.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<Tag> CreateAsync(
        CreateTagRequest request,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        var id = AccountId(accountId);
        return CallAsync<Tag>(
            $"accounts/{id}/tags",
            HttpMethod.Post,
            request,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>PUT /accounts/{account_id}/tags/{tag_id}</c> — update a tag's name and/or color. The API returns <c>409 Conflict</c> if the new name collides with another tag.</summary>
    /// <param name="tagId">Tag to update.</param>
    /// <param name="request">New name and/or color; unset properties leave the existing value unchanged.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<Tag> UpdateAsync(
        string tagId,
        UpdateTagRequest request,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = AccountId(accountId);
        var tag = RequireId(tagId, "Tag ID");

        return CallAsync<Tag>(
            $"accounts/{id}/tags/{tag}",
            HttpMethod.Put,
            request,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// <c>DELETE /accounts/{account_id}/tags/{tag_id}</c> — delete a tag. When <paramref name="force"/>
    /// is <see langword="false"/> the API returns <c>409 Conflict</c> if the tag is still attached to
    /// documents or templates; pass <see langword="true"/> to detach it everywhere and delete it.
    /// </summary>
    /// <param name="tagId">Tag to delete.</param>
    /// <param name="force">When <see langword="true"/>, detach the tag from every document and template before deleting; when <see langword="false"/> the API returns <c>409 Conflict</c> if the tag is still attached.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task DeleteAsync(
        string tagId,
        bool force = false,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var tag = RequireId(tagId, "Tag ID");

        var path = AppendQueryString(
            $"accounts/{id}/tags/{tag}",
            force ? new Dictionary<string, string?> { ["force"] = "true" } : null);

        return CallVoidAsync(path, HttpMethod.Delete, cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /accounts/{account_id}/documents/{document_id}/tags</c> — list the tags currently attached to a document.</summary>
    /// <param name="documentId">Document whose attached tags to list.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<Tag>> ListForDocumentAsync(
        string documentId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var document = RequireId(documentId, "Document ID");

        return await CallListBodyAsync<Tag>(
            $"accounts/{id}/documents/{document}/tags",
            HttpMethod.Get,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary><c>POST /accounts/{account_id}/documents/{document_id}/tags</c> — attach tags to a document, keeping any already attached. Tags are referenced by name and created on the fly if they do not exist.</summary>
    /// <param name="documentId">Document to attach tags to.</param>
    /// <param name="tags">Tag names to attach; any that do not yet exist are created.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<IReadOnlyList<Tag>> AddToDocumentAsync(
        string documentId,
        IReadOnlyList<string> tags,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        return SendDocumentTagsAsync(documentId, tags, HttpMethod.Post, accountId, cancellationToken);
    }

    /// <summary><c>PUT /accounts/{account_id}/documents/{document_id}/tags</c> — replace a document's tags with exactly the supplied set (pass an empty list to clear all).</summary>
    /// <param name="documentId">Document whose tags to replace.</param>
    /// <param name="tags">Exact set of tag names the document should have; pass an empty list to clear all.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<IReadOnlyList<Tag>> SetForDocumentAsync(
        string documentId,
        IReadOnlyList<string> tags,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        return SendDocumentTagsAsync(documentId, tags, HttpMethod.Put, accountId, cancellationToken);
    }

    /// <summary>
    /// <c>DELETE /accounts/{account_id}/documents/{document_id}/tags/{tag_id}</c> — detach a single
    /// tag from a document without deleting the tag itself. Note: unlike
    /// <see cref="AddToDocumentAsync"/> / <see cref="SetForDocumentAsync"/> (which key tags by name),
    /// detach is keyed by tag <b>id</b>; resolve a name to its id via <see cref="ListForDocumentAsync"/> first.
    /// </summary>
    /// <param name="documentId">Document to detach the tag from.</param>
    /// <param name="tagId">Tag to detach, keyed by tag id (not name).</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task RemoveFromDocumentAsync(
        string documentId,
        string tagId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var document = RequireId(documentId, "Document ID");
        var tag = RequireId(tagId, "Tag ID");

        return CallVoidAsync(
            $"accounts/{id}/documents/{document}/tags/{tag}",
            HttpMethod.Delete,
            cancellationToken: cancellationToken);
    }

    private async Task<IReadOnlyList<Tag>> SendDocumentTagsAsync(
        string documentId,
        IReadOnlyList<string> tags,
        HttpMethod method,
        string? accountId,
        CancellationToken cancellationToken)
    {
        var id = AccountId(accountId);
        var document = RequireId(documentId, "Document ID");
        ArgumentNullException.ThrowIfNull(tags);

        return await CallListBodyAsync<Tag>(
            $"accounts/{id}/documents/{document}/tags",
            method,
            new Dictionary<string, object?> { ["tags"] = tags },
            cancellationToken).ConfigureAwait(false);
    }
}
