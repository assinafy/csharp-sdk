using System.Net.Http.Headers;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Documents resource: upload, list, get, download, verify, and activities.</summary>
public sealed class DocumentResource : BaseResource
{
    private const long MaximumFileSizeBytes = 25L * 1024L * 1024L;

    private static readonly IReadOnlySet<string> ReadyStatuses = new HashSet<string>
    {
        "metadata_ready", "pending_signature", "certificated",
    };

    private static readonly IReadOnlySet<string> FailedStatuses = new HashSet<string>
    {
        "failed", "rejected_by_signer", "rejected_by_user", "expired",
    };

    internal DocumentResource(HttpClient http, string? defaultAccountId = null, Action<HttpRequestMessage>? authenticate = null)
        : base(http, defaultAccountId, authenticate) { }

    /// <summary><c>GET /documents/statuses</c> — list all possible document status codes and whether each is deletable.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<IReadOnlyList<DocumentStatusInfo>> ListStatusesAsync(
        CancellationToken cancellationToken = default)
    {
        return CallListBodyAsync<DocumentStatusInfo>(
            "documents/statuses",
            HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// <c>POST /accounts/{account_id}/documents</c> — upload a PDF to a workspace.
    /// This method validates the file extension and a 25MB size cap client-side; the API
    /// additionally enforces a 2000-page limit server-side (a smaller-but-longer PDF is
    /// rejected by the API rather than locally).
    /// </summary>
    /// <param name="fileStream">The PDF file content to upload.</param>
    /// <param name="fileName">File name for the upload; must end in <c>.pdf</c> (case-insensitive).</param>
    /// <param name="accountId">Workspace account to upload into; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DocumentDetails> UploadAsync(
        Stream fileStream,
        string fileName,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException(
                "Only PDF files are supported.",
                new Dictionary<string, object?> { ["fileName"] = fileName });

        if (fileStream.CanSeek && fileStream.Length > MaximumFileSizeBytes)
            throw new ValidationException(
                "Document file size must not exceed 25MB.",
                new Dictionary<string, object?> { ["fileName"] = fileName, ["size"] = fileStream.Length });

        var id = AccountId(accountId);

        using var content = new MultipartFormDataContent();
        var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(streamContent, "file", fileName);

        var result = await CallContentAsync<DocumentDetails>(
            $"accounts/{id}/documents",
            HttpMethod.Post,
            content,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (result?.Id is null)
            throw new ValidationException("Upload succeeded but no document ID was returned.");

        return result;
    }

    /// <summary><c>GET /accounts/{account_id}/documents</c> — list documents in the workspace with optional filters (<c>status</c>, <c>method</c>, <c>search</c>, <c>sort</c>, <c>page</c>, <c>per-page</c>).</summary>
    /// <param name="queryParams">
    /// Optional filters. Accepted keys: <c>status</c> (a document status code, e.g. <c>pending_signature</c>);
    /// <c>method</c> (<c>virtual</c> or <c>collect</c>, see <see cref="AssignmentMethods"/>);
    /// <c>search</c> (partial match on document name or a signer's name/email); <c>tags</c> (comma-separated tag IDs,
    /// returning documents that carry all of them); <c>sort</c> (<c>name</c> or <c>updated_at</c>);
    /// <c>page</c> (1-based page number); and <c>per-page</c> (page size).
    /// </param>
    /// <param name="accountId">Workspace account whose documents to list; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<PaginatedResult<DocumentListItem>> ListAsync(
        IDictionary<string, string?>? queryParams = null,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        return CallListAsync<DocumentListItem>($"accounts/{id}/documents", queryParams, cancellationToken);
    }

    /// <summary><c>GET /documents/{document_id}</c> — fetch full document details including assignment and artifacts.</summary>
    /// <param name="documentId">Document to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<DocumentDetails> GetAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var id = RequireId(documentId, "Document ID");
        return CallAsync<DocumentDetails>($"documents/{id}", HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>DELETE /documents/{documentId}</c> — delete a document. Only certain status codes are deletable (see <see cref="ListStatusesAsync"/>).</summary>
    /// <param name="documentId">Document to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task DeleteAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var id = RequireId(documentId, "Document ID");
        return CallVoidAsync($"documents/{id}", HttpMethod.Delete,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// <c>PATCH /documents/{document_id}</c> — rename a document. Only permitted before any assignment
    /// exists (status <c>uploaded</c> or <c>metadata_ready</c> with no signers); once signing has started
    /// or the document is certificated the name is locked. The server normalizes the name (diacritics are
    /// removed and unsupported characters are replaced with dashes), so the returned name may differ.
    /// </summary>
    /// <param name="documentId">Document to rename.</param>
    /// <param name="name">The new document name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<DocumentDetails> RenameAsync(
        string documentId,
        string name,
        CancellationToken cancellationToken = default)
    {
        var id = RequireId(documentId, "Document ID");
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return CallAsync<DocumentDetails>(
            $"documents/{id}",
            HttpMethod.Patch,
            new Dictionary<string, object?> { ["name"] = name },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// <c>GET /accounts/{account_id}/documents/search</c> — search documents by name, returning a compact
    /// representation (without the expanded <c>assignment</c> and <c>pages</c> that <see cref="ListAsync"/>
    /// includes). Use <see cref="ListAsync"/> when you need the full document shape.
    /// </summary>
    /// <param name="search">Case-insensitive search term matched against document names.</param>
    /// <param name="status">Optional document status filter.</param>
    /// <param name="page">Optional 1-based page number.</param>
    /// <param name="perPage">Optional page size.</param>
    /// <param name="accountId">Workspace account; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<PaginatedResult<DocumentListItem>> SearchAsync(
        string? search = null,
        string? status = null,
        int? page = null,
        int? perPage = null,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var query = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(search)) query["search"] = search;
        if (!string.IsNullOrWhiteSpace(status)) query["status"] = status;
        if (page is int p) query["page"] = p.ToString();
        if (perPage is int pp) query["per-page"] = pp.ToString();

        return CallListAsync<DocumentListItem>($"accounts/{id}/documents/search", query, cancellationToken);
    }

    /// <summary><c>GET /documents/{document_id}/download/{artifact_name}</c> — download a document artifact (<c>original</c>, <c>certificated</c>, <c>certificate-page</c>, or <c>bundle</c>).</summary>
    /// <param name="documentId">Document whose artifact to download.</param>
    /// <param name="artifactName">Which artifact to download — <c>original</c>, <c>certificated</c>, <c>certificate-page</c>, or <c>bundle</c> (see <see cref="DocumentArtifactNames"/>); defaults to <c>certificated</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw artifact bytes.</returns>
    public Task<byte[]> DownloadAsync(
        string documentId,
        string artifactName = DocumentArtifactNames.Certificated,
        CancellationToken cancellationToken = default)
    {
        var id = RequireId(documentId, "Document ID");
        var artifact = RequireId(artifactName, "Artifact name");
        return CallBinaryAsync($"documents/{id}/download/{artifact}", HttpMethod.Get, cancellationToken);
    }

    /// <summary><c>GET /documents/{document_id}/thumbnail</c> — download the first-page thumbnail image.</summary>
    /// <param name="documentId">Document whose thumbnail to download.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw thumbnail image bytes.</returns>
    public Task<byte[]> ThumbnailAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var id = RequireId(documentId, "Document ID");
        return CallBinaryAsync($"documents/{id}/thumbnail", HttpMethod.Get, cancellationToken);
    }

    /// <summary><c>GET /documents/{document_id}/pages/{page_id}/download</c> — download a single page rendering.</summary>
    /// <param name="documentId">Document that owns the page.</param>
    /// <param name="pageId">Page whose rendering to download.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw page image bytes.</returns>
    public Task<byte[]> DownloadPageAsync(
        string documentId,
        string pageId,
        CancellationToken cancellationToken = default)
    {
        var docId = RequireId(documentId, "Document ID");
        var pid = RequireId(pageId, "Page ID");
        return CallBinaryAsync($"documents/{docId}/pages/{pid}/download", HttpMethod.Get, cancellationToken);
    }

    /// <summary><c>GET /documents/{documentId}/activities</c> — fetch the timeline of events recorded against this document.</summary>
    /// <param name="documentId">Document whose activity timeline to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<DocumentActivity>> ActivitiesAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var id = RequireId(documentId, "Document ID");
        return await CallListBodyAsync<DocumentActivity>(
            $"documents/{id}/activities",
            HttpMethod.Get,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Convenience helper: poll <see cref="GetAsync"/> until the document reaches
    /// a "ready" status (<c>metadata_ready</c>, <c>pending_signature</c>, or
    /// <c>certificated</c>), throws if it lands in a failed/expired state, or
    /// throws on timeout.
    /// </summary>
    /// <param name="documentId">Document to poll.</param>
    /// <param name="maxWait">Maximum time to wait before throwing on timeout (default 30 seconds).</param>
    /// <param name="pollInterval">Delay between status polls (default 2 seconds).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<DocumentDetails> WaitUntilReadyAsync(
        string documentId,
        TimeSpan? maxWait = null,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        var id = RequireId(documentId, "Document ID");
        var deadline = DateTime.UtcNow + (maxWait ?? TimeSpan.FromSeconds(30));
        var interval = pollInterval ?? TimeSpan.FromSeconds(2);
        var attempts = 0;

        while (DateTime.UtcNow < deadline)
        {
            attempts++;
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var details = await GetAsync(id, cancellationToken).ConfigureAwait(false);
                if (ReadyStatuses.Contains(details.Status)) return details;
                if (FailedStatuses.Contains(details.Status))
                    throw new ValidationException($"Document processing failed with status: {details.Status}");
            }
            catch (NetworkException) { /* transient — retry until deadline */ }
            catch (ApiException ex) when (ex.StatusCode >= 500) { /* transient — retry until deadline */ }
            catch (ApiException ex) when (ex.StatusCode == 404 && attempts <= 3)
            {
                /* freshly created document may not be queryable yet — retry briefly */
            }

            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
        }

        throw new ValidationException(
            "Timeout waiting for document to be ready.",
            new Dictionary<string, object?> { ["documentId"] = id, ["attempts"] = attempts });
    }

    /// <summary>Convenience helper: returns true if the document is fully signed by every signer.</summary>
    /// <param name="documentId">Document to inspect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<bool> IsFullySignedAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var details = await GetAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (details.Status == "certificated") return true;

        var summary = details.Assignment?.Summary;
        if (summary is not null)
            return summary.SignerCount > 0 && summary.SignerCount == summary.CompletedCount;

        var signers = details.Assignment?.Signers;
        return signers is { Count: > 0 } && signers.All(s => s.Completed == true);
    }

    /// <summary>Convenience helper: returns a (signed / total / pending / percentage) snapshot.</summary>
    /// <param name="documentId">Document to inspect.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<SigningProgress> GetSigningProgressAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var details = await GetAsync(documentId, cancellationToken).ConfigureAwait(false);
        var summary = details.Assignment?.Summary;
        var signers = details.Assignment?.Signers;
        var total = summary?.SignerCount ?? signers?.Count ?? 0;
        var signed = summary?.CompletedCount ?? signers?.Count(s => s.Completed == true) ?? 0;
        var pending = Math.Max(total - signed, 0);
        var percentage = total > 0 ? Math.Round((double)signed / total * 10000) / 100 : 0;

        return new SigningProgress
        {
            Signed = signed,
            Total = total,
            Pending = pending,
            Percentage = percentage,
        };
    }

    /// <summary><c>POST /accounts/{account_id}/templates/{template_id}/documents</c> — create a document by binding signers to template roles.</summary>
    /// <param name="templateId">Template to instantiate.</param>
    /// <param name="signers">Signers bound to the template's roles.</param>
    /// <param name="options">Optional document name, message, <c>expires_at</c>, and editor-field values.</param>
    /// <param name="accountId">Workspace account to create the document in; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<DocumentDetails> CreateFromTemplateAsync(
        string templateId,
        IReadOnlyList<TemplateSigner> signers,
        CreateDocumentFromTemplateOptions? options = null,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var template = RequireId(templateId, "Template ID");
        var account = AccountId(accountId);
        ArgumentNullException.ThrowIfNull(signers);

        var body = new Dictionary<string, object?>
        {
            ["signers"] = signers,
        };

        if (options?.Name is not null) body["name"] = options.Name;
        if (options?.Message is not null) body["message"] = options.Message;
        if (options?.ExpiresAt is not null) body["expires_at"] = options.ExpiresAt;
        if (options?.EditorFields is not null) body["editor_fields"] = options.EditorFields;

        return CallAsync<DocumentDetails>(
            $"accounts/{account}/templates/{template}/documents",
            HttpMethod.Post,
            body,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>POST /accounts/{account_id}/templates/{template_id}/documents/estimate-cost</c> — preview the credit cost of <see cref="CreateFromTemplateAsync"/>.</summary>
    /// <param name="templateId">Template that would be instantiated.</param>
    /// <param name="signers">Signers that would be bound to the template's roles.</param>
    /// <param name="accountId">Workspace account context; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<AssignmentCostEstimate> EstimateCostFromTemplateAsync(
        string templateId,
        IReadOnlyList<TemplateSigner> signers,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var template = RequireId(templateId, "Template ID");
        var account = AccountId(accountId);
        ArgumentNullException.ThrowIfNull(signers);

        return CallAsync<AssignmentCostEstimate>(
            $"accounts/{account}/templates/{template}/documents/estimate-cost",
            HttpMethod.Post,
            new { signers },
            cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /documents/{signature_hash}/verify</c> — verify a document's signature hash and return validity metadata.</summary>
    /// <param name="signatureHash">The document's public signature hash to verify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<DocumentVerificationResult> VerifyAsync(
        string signatureHash,
        CancellationToken cancellationToken = default)
    {
        var hash = RequireId(signatureHash, "Signature hash");
        return CallAsync<DocumentVerificationResult>($"documents/{hash}/verify", HttpMethod.Get,
            cancellationToken: cancellationToken);
    }
}
