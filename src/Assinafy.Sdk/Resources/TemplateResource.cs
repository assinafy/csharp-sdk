using System.Net.Http.Headers;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Support;

namespace Assinafy.Sdk.Resources;

/// <summary>Account template creation, listing, inspection, updates, deletion, and rendered-page downloads.</summary>
public sealed class TemplateResource : BaseResource
{
    private const long MaximumFileSizeBytes = 25L * 1024L * 1024L;

    internal TemplateResource(HttpClient http, string? defaultAccountId = null, Action<HttpRequestMessage>? authenticate = null)
        : base(http, defaultAccountId, authenticate) { }

    /// <summary>
    /// <c>POST /accounts/{account_id}/templates</c> — create a reusable template from a PDF.
    /// The API derives the template name from the multipart file name, so <paramref name="name"/>
    /// replaces that name and receives a <c>.pdf</c> suffix when needed. The returned template can
    /// remain in an upload or processing status until its pages are ready. For seekable streams,
    /// the 25MB cap is checked against the bytes remaining from the current position; the API
    /// enforces its upload limit for every stream.
    /// </summary>
    /// <param name="fileStream">The PDF file content to upload.</param>
    /// <param name="fileName">Source file name; must end in <c>.pdf</c> (case-insensitive).</param>
    /// <param name="name">Optional template display name. It is sent as the multipart file name, not as a separate form field.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created template, including its status, pages, roles, and tags.</returns>
    public async Task<TemplateDetails> CreateAsync(
        Stream fileStream,
        string fileName,
        string? name = null,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException(
                "Only PDF files are supported.",
                new Dictionary<string, object?> { ["fileName"] = fileName });

        if (fileStream.CanSeek)
        {
            var remainingBytes = fileStream.Length - fileStream.Position;
            if (remainingBytes > MaximumFileSizeBytes)
                throw new ValidationException(
                    "Template file size must not exceed 25MB.",
                    new Dictionary<string, object?> { ["fileName"] = fileName, ["size"] = remainingBytes });
        }

        if (name is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var uploadName = name is null || name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)
            ? name ?? fileName
            : $"{name}.pdf";
        var id = AccountId(accountId);

        using var content = new MultipartFormDataContent();
        var streamContent = new NonDisposingStreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(streamContent, "file", uploadName);

        var result = await CallContentAsync<TemplateDetails>(
            $"accounts/{id}/templates",
            HttpMethod.Post,
            content,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(result.Id))
            throw new SerializationException("Template upload succeeded but no template ID was returned.");

        return result;
    }

    /// <summary><c>GET /accounts/{account_id}/templates</c> — list templates in a workspace with optional <c>search</c>, <c>page</c>, and <c>per-page</c> filters.</summary>
    /// <param name="queryParams">Optional <c>search</c>, <c>page</c>, and <c>per-page</c> query filters.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated collection of templates.</returns>
    public Task<PaginatedResult<TemplateListItem>> ListAsync(
        IDictionary<string, string?>? queryParams = null,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        return CallListAsync<TemplateListItem>(
            $"accounts/{id}/templates",
            queryParams,
            cancellationToken);
    }

    /// <summary><c>GET /accounts/{account_id}/templates/{template_id}</c> — fetch template details including roles, pages, and field placements.</summary>
    /// <param name="templateId">Template to fetch.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The requested template, including its roles, pages, fields, and tags.</returns>
    public Task<TemplateDetails> GetAsync(
        string templateId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var template = PathSegment(templateId, "Template ID");
        return CallAsync<TemplateDetails>(
            $"accounts/{id}/templates/{template}",
            HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// <c>PUT /accounts/{account_id}/templates/{template_id}</c> — update a template's display name
    /// and/or default invitation message. Properties left <see langword="null"/> are omitted.
    /// </summary>
    /// <param name="templateId">Template to update.</param>
    /// <param name="request">New name and/or default invitation message.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated template, including its status, pages, roles, and tags.</returns>
    public Task<TemplateDetails> UpdateAsync(
        string templateId,
        UpdateTemplateRequest request,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = AccountId(accountId);
        var template = PathSegment(templateId, "Template ID");

        return CallAsync<TemplateDetails>(
            $"accounts/{id}/templates/{template}",
            HttpMethod.Put,
            request,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>DELETE /accounts/{account_id}/templates/{template_id}</c> — permanently delete a reusable template.</summary>
    /// <param name="templateId">Template to delete.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the template has been deleted.</returns>
    public Task DeleteAsync(
        string templateId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var template = PathSegment(templateId, "Template ID");
        return CallVoidAsync(
            $"accounts/{id}/templates/{template}",
            HttpMethod.Delete,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /accounts/{account_id}/templates/{template_id}/pages/{page_id}/download</c> — download a rendered template page as JPEG bytes.</summary>
    /// <param name="templateId">Template that owns the page.</param>
    /// <param name="pageId">Page to download.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw JPEG bytes.</returns>
    public Task<byte[]> DownloadPageAsync(
        string templateId,
        string pageId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var template = PathSegment(templateId, "Template ID");
        var page = PathSegment(pageId, "Page ID");
        return CallBinaryAsync(
            $"accounts/{id}/templates/{template}/pages/{page}/download",
            HttpMethod.Get,
            cancellationToken);
    }
}
