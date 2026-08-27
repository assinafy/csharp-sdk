using Assinafy.Sdk.Exceptions;
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
    /// <returns>The workspace tags matching the optional search.</returns>
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
    /// <returns>The newly created tag.</returns>
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
    /// <returns>The updated tag.</returns>
    public Task<Tag> UpdateAsync(
        string tagId,
        UpdateTagRequest request,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ClearColor && request.Color is not null)
            throw new ValidationException("Color and ClearColor cannot both be set.");

        var id = AccountId(accountId);
        var tag = PathSegment(tagId, "Tag ID");

        var body = new Dictionary<string, object?>();
        if (request.Name is not null) body["name"] = request.Name;
        if (request.ClearColor) body["color"] = null;
        else if (request.Color is not null) body["color"] = request.Color;

        return CallAsync<Tag>(
            $"accounts/{id}/tags/{tag}",
            HttpMethod.Put,
            body,
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
    /// <returns>A task that completes when the tag has been deleted.</returns>
    public Task DeleteAsync(
        string tagId,
        bool force = false,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var tag = PathSegment(tagId, "Tag ID");

        var path = AppendQueryString(
            $"accounts/{id}/tags/{tag}",
            force ? new Dictionary<string, string?> { ["force"] = "true" } : null);

        return CallVoidAsync(path, HttpMethod.Delete, cancellationToken: cancellationToken);
    }

    /// <summary><c>DELETE /accounts/{account_id}/tags/{tag_id}</c> — delete a tag and return the API's <c>{"deleted":boolean}</c> payload.</summary>
    /// <param name="tagId">Tag to delete.</param>
    /// <param name="force">Whether to detach the tag everywhere before deleting it.</param>
    /// <param name="accountId">Workspace account; falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The API deletion result.</returns>
    public Task<DeleteTagResult> DeleteWithResultAsync(
        string tagId,
        bool force = false,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var tag = PathSegment(tagId, "Tag ID");
        var path = AppendQueryString(
            $"accounts/{id}/tags/{tag}",
            force ? new Dictionary<string, string?> { ["force"] = "true" } : null);
        return CallAsync<DeleteTagResult>(path, HttpMethod.Delete, cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /accounts/{account_id}/documents/{document_id}/tags</c> — list the tags currently attached to a document.</summary>
    /// <param name="documentId">Document whose attached tags to list.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The tags currently attached to the document.</returns>
    public async Task<IReadOnlyList<Tag>> ListForDocumentAsync(
        string documentId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var document = PathSegment(documentId, "Document ID");

        return await CallListBodyAsync<Tag>(
            $"accounts/{id}/documents/{document}/tags",
            HttpMethod.Get,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary><c>POST /accounts/{account_id}/documents/{document_id}/tags</c> — attach existing tags to a document, keeping any already attached.</summary>
    /// <param name="documentId">Document to attach tags to.</param>
    /// <param name="tags">Tag IDs to attach. Create missing tags first with <see cref="CreateAsync"/>.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document's attached tags after the additions.</returns>
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
    /// <param name="tags">Exact set of tag IDs the document should have; pass an empty list to clear all.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document's complete replacement set of tags.</returns>
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
    /// tag from a document without deleting the tag itself.
    /// </summary>
    /// <param name="documentId">Document to detach the tag from.</param>
    /// <param name="tagId">Tag ID to detach.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the tag has been detached.</returns>
    public Task RemoveFromDocumentAsync(
        string documentId,
        string tagId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var document = PathSegment(documentId, "Document ID");
        var tag = PathSegment(tagId, "Tag ID");

        return CallVoidAsync(
            $"accounts/{id}/documents/{document}/tags/{tag}",
            HttpMethod.Delete,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>DELETE /accounts/{account_id}/documents/{document_id}/tags/{tag_id}</c> — detach a tag and return the API's <c>{"detached":boolean}</c> payload.</summary>
    /// <param name="documentId">Document to detach from.</param>
    /// <param name="tagId">Tag ID to detach.</param>
    /// <param name="accountId">Workspace account; falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The API detach result.</returns>
    public Task<DetachTagResult> RemoveFromDocumentWithResultAsync(
        string documentId,
        string tagId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var document = PathSegment(documentId, "Document ID");
        var tag = PathSegment(tagId, "Tag ID");
        return CallAsync<DetachTagResult>(
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
        var document = PathSegment(documentId, "Document ID");
        ArgumentNullException.ThrowIfNull(tags);

        return await CallListBodyAsync<Tag>(
            $"accounts/{id}/documents/{document}/tags",
            method,
            new Dictionary<string, object?> { ["tags"] = tags },
            cancellationToken).ConfigureAwait(false);
    }
}
