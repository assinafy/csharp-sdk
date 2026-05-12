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

    internal DocumentResource(HttpClient http, string? defaultAccountId = null)
        : base(http, defaultAccountId) { }

    /// <summary><c>GET /documents/statuses</c> — list all possible document status codes and whether each is deletable.</summary>
    public async Task<IReadOnlyList<DocumentStatusInfo>> ListStatusesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await CallAsync<List<DocumentStatusInfo>>(
            "documents/statuses",
            HttpMethod.Get,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result ?? [];
    }

    /// <summary>
    /// <c>POST /accounts/{account_id}/documents</c> — upload a PDF to a workspace.
    /// Only PDF files are accepted; the API also enforces a 25MB / 2000-page limit.
    /// </summary>
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
    public Task<PaginatedResult<DocumentListItem>> ListAsync(
        IDictionary<string, string?>? queryParams = null,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        return CallListAsync<DocumentListItem>($"accounts/{id}/documents", queryParams, cancellationToken);
    }

    /// <summary><c>GET /documents/{document_id}</c> — fetch full document details including assignment and artifacts.</summary>
    public Task<DocumentDetails> GetAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var id = RequireId(documentId, "Document ID");
        return CallAsync<DocumentDetails>($"documents/{id}", HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>DELETE /documents/{documentId}</c> — delete a document. Only certain status codes are deletable (see <see cref="ListStatusesAsync"/>).</summary>
    public Task DeleteAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var id = RequireId(documentId, "Document ID");
        return CallVoidAsync($"documents/{id}", HttpMethod.Delete,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /documents/{document_id}/download/{artifact_name}</c> — download a document artifact (<c>original</c>, <c>certificated</c>, <c>certificate-page</c>, or <c>bundle</c>).</summary>
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
    public Task<byte[]> ThumbnailAsync(string documentId, CancellationToken cancellationToken = default)
    {
        var id = RequireId(documentId, "Document ID");
        return CallBinaryAsync($"documents/{id}/thumbnail", HttpMethod.Get, cancellationToken);
    }

    /// <summary><c>GET /documents/{document_id}/pages/{page_id}/download</c> — download a single page rendering.</summary>
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
    public async Task<IReadOnlyList<DocumentActivity>> ActivitiesAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var id = RequireId(documentId, "Document ID");
        var result = await CallAsync<List<DocumentActivity>>(
            $"documents/{id}/activities",
            HttpMethod.Get,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result ?? [];
    }

    /// <summary>
    /// Convenience helper: poll <see cref="GetAsync"/> until the document reaches
    /// a "ready" status (<c>metadata_ready</c>, <c>pending_signature</c>, or
    /// <c>certificated</c>), throws if it lands in a failed/expired state, or
    /// throws on timeout.
    /// </summary>
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

            await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
        }

        throw new ValidationException(
            "Timeout waiting for document to be ready.",
            new Dictionary<string, object?> { ["documentId"] = id, ["attempts"] = attempts });
    }

    /// <summary>Convenience helper: returns true if the document is fully signed by every signer.</summary>
    public async Task<bool> IsFullySignedAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var details = await GetAsync(documentId, cancellationToken).ConfigureAwait(false);
        if (details.Status == "certificated") return true;

        var summary = details.Assignment?.Summary;
        return summary is not null
            && summary.SignerCount > 0
            && summary.SignerCount == summary.CompletedCount;
    }

    /// <summary>Convenience helper: returns a (signed / total / pending / percentage) snapshot.</summary>
    public async Task<SigningProgress> GetSigningProgressAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var details = await GetAsync(documentId, cancellationToken).ConfigureAwait(false);
        var summary = details.Assignment?.Summary;
        var total = summary?.SignerCount ?? details.Assignment?.Signers.Count ?? 0;
        var signed = summary?.CompletedCount ?? 0;
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
    public Task<DocumentVerificationResult> VerifyAsync(
        string signatureHash,
        CancellationToken cancellationToken = default)
    {
        var hash = RequireId(signatureHash, "Signature hash");
        return CallAsync<DocumentVerificationResult>($"documents/{hash}/verify", HttpMethod.Get,
            cancellationToken: cancellationToken);
    }
}
