using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Signer-facing document access, signing, declining, and downloads.</summary>
public sealed class SigningResource : BaseResource
{
    internal SigningResource(HttpClient http, Action<HttpRequestMessage>? authenticate = null)
        : base(http, authenticate: authenticate) { }

    /// <summary><c>GET /sign</c> — signer-facing endpoint: load the document and assignment data for the current signer access code.</summary>
    /// <param name="signerAccessCode">The signer's access code identifying which assignment to load.</param>
    /// <param name="hasAcceptedTerms">When set, records whether the signer has accepted the terms of use; sent as the <c>has_accepted_terms</c> query flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<DocumentDetails> GetAsync(
        string signerAccessCode,
        bool? hasAcceptedTerms = null,
        CancellationToken cancellationToken = default)
    {
        var code = RequireId(signerAccessCode, "Signer access code");
        var query = AccessCodeQuery(code);

        if (hasAcceptedTerms.HasValue)
            query["has_accepted_terms"] = hasAcceptedTerms.Value ? "true" : "false";

        return CallAsync<DocumentDetails>(
            AppendQueryString("sign", query),
            HttpMethod.Get,
            cancellationToken: cancellationToken,
            authenticate: false);
    }

    /// <summary>
    /// <c>POST /documents/{documentId}/assignments/{assignmentId}?signer-access-code={code}</c>
    /// — submit a signer's field values. For virtual assignments the signer must call
    /// <see cref="SignerResource.ConfirmDataAsync"/> first; otherwise the API returns 400.
    /// The body uses camelCase keys (<c>itemId</c>, <c>fieldId</c>, <c>pageId</c>, <c>value</c>)
    /// per the Assinafy docs, which <see cref="SignAssignmentValue"/> applies automatically.
    /// </summary>
    /// <param name="documentId">Document containing the assignment.</param>
    /// <param name="assignmentId">Assignment whose field values are being submitted.</param>
    /// <param name="signerAccessCode">The signer's access code authorizing the submission.</param>
    /// <param name="values">Field values to submit, one entry per field; serialized with camelCase keys.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task SignAsync(
        string documentId,
        string assignmentId,
        string signerAccessCode,
        IReadOnlyList<SignAssignmentValue> values,
        CancellationToken cancellationToken = default)
    {
        var document = RequireId(documentId, "Document ID");
        var assignment = RequireId(assignmentId, "Assignment ID");
        var code = RequireId(signerAccessCode, "Signer access code");
        ArgumentNullException.ThrowIfNull(values);

        var path = AppendQueryString(
            $"documents/{document}/assignments/{assignment}",
            AccessCodeQuery(code));

        return CallVoidAsync(
            path,
            HttpMethod.Post,
            values,
            cancellationToken,
            authenticate: false);
    }

    /// <summary><c>PUT /documents/{documentId}/assignments/{assignmentId}/reject?signer-access-code={code}</c> — signer-facing endpoint: decline an assignment with a reason.</summary>
    /// <param name="documentId">Document containing the assignment.</param>
    /// <param name="assignmentId">Assignment being declined.</param>
    /// <param name="signerAccessCode">The signer's access code authorizing the decline.</param>
    /// <param name="declineReason">Reason the signer is declining the assignment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task DeclineAsync(
        string documentId,
        string assignmentId,
        string signerAccessCode,
        string declineReason,
        CancellationToken cancellationToken = default)
    {
        var document = RequireId(documentId, "Document ID");
        var assignment = RequireId(assignmentId, "Assignment ID");
        var code = RequireId(signerAccessCode, "Signer access code");
        ArgumentException.ThrowIfNullOrWhiteSpace(declineReason);

        var path = AppendQueryString(
            $"documents/{document}/assignments/{assignment}/reject",
            AccessCodeQuery(code));

        return CallVoidAsync(
            path,
            HttpMethod.Put,
            new DeclineAssignmentRequest { DeclineReason = declineReason },
            cancellationToken,
            authenticate: false);
    }

    /// <summary><c>GET /signers/{signer_id}/document?signer-access-code={code}</c> — fetch the signer's current document.</summary>
    /// <param name="signerId">Signer whose current document to fetch.</param>
    /// <param name="signerAccessCode">The signer's access code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<DocumentDetails> GetCurrentDocumentAsync(
        string signerId,
        string signerAccessCode,
        CancellationToken cancellationToken = default)
    {
        var signer = RequireId(signerId, "Signer ID");
        var code = RequireId(signerAccessCode, "Signer access code");

        var path = AppendQueryString(
            $"signers/{signer}/document",
            AccessCodeQuery(code));

        return CallAsync<DocumentDetails>(
            path,
            HttpMethod.Get,
            cancellationToken: cancellationToken,
            authenticate: false);
    }

    /// <summary><c>GET /signers/{signer_id}/documents?signer-access-code={code}</c> — list all documents associated with the signer.</summary>
    /// <param name="signerId">Signer whose documents to list.</param>
    /// <param name="signerAccessCode">The signer's access code.</param>
    /// <param name="parameters">Optional pagination. The documented endpoint accepts <c>Page</c> and <c>PerPage</c>; explicitly set legacy filters are retained for compatibility.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<PaginatedResult<DocumentListItem>> ListDocumentsAsync(
        string signerId,
        string signerAccessCode,
        SignerDocumentListParams? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var signer = RequireId(signerId, "Signer ID");
        var code = RequireId(signerAccessCode, "Signer access code");

        var query = BuildPaginationQuery(parameters);
        query[SignerAccessCodeParam] = code;

        return CallListAsync<DocumentListItem>(
            $"signers/{signer}/documents",
            query,
            cancellationToken,
            authenticate: false);
    }

    /// <summary>
    /// <c>GET /signers/{signer_id}/documents/search?signer-access-code={code}</c> — search the signer's
    /// documents by name, returning a compact representation. Use <see cref="ListDocumentsAsync"/> when you
    /// need the full document shape or pagination.
    /// </summary>
    /// <param name="signerId">Signer whose documents to search.</param>
    /// <param name="signerAccessCode">The signer's access code.</param>
    /// <param name="parameters">Optional search term. The documented endpoint accepts <c>Search</c>; explicitly set legacy filters and pagination are retained for compatibility.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<PaginatedResult<DocumentListItem>> SearchDocumentsAsync(
        string signerId,
        string signerAccessCode,
        SignerDocumentListParams? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var signer = RequireId(signerId, "Signer ID");
        var code = RequireId(signerAccessCode, "Signer access code");

        var query = BuildSearchQuery(parameters);
        query[SignerAccessCodeParam] = code;

        return CallListAsync<DocumentListItem>(
            $"signers/{signer}/documents/search",
            query,
            cancellationToken,
            authenticate: false);
    }

    /// <summary><c>PUT /signers/documents/sign-multiple?signer-access-code={code}</c> — sign multiple virtual-method documents in one request.</summary>
    /// <param name="signerAccessCode">The signer's access code authorizing the signatures.</param>
    /// <param name="documentIds">Documents to sign; must contain at least one id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task SignMultipleAsync(
        string signerAccessCode,
        IReadOnlyList<string> documentIds,
        CancellationToken cancellationToken = default)
    {
        var code = RequireId(signerAccessCode, "Signer access code");
        ArgumentNullException.ThrowIfNull(documentIds);
        if (documentIds.Count == 0)
            throw new ValidationException("At least one document ID is required.");

        var path = AppendQueryString(
            "signers/documents/sign-multiple",
            AccessCodeQuery(code));

        return CallVoidAsync(
            path,
            HttpMethod.Put,
            new SignMultipleDocumentsRequest { DocumentIds = documentIds },
            cancellationToken,
            authenticate: false);
    }

    /// <summary><c>PUT /signers/documents/decline-multiple?signer-access-code={code}</c> — decline multiple documents at once with a single reason.</summary>
    /// <param name="signerAccessCode">The signer's access code authorizing the declines.</param>
    /// <param name="documentIds">Documents to decline; must contain at least one id.</param>
    /// <param name="declineReason">Reason applied to every declined document.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task DeclineMultipleAsync(
        string signerAccessCode,
        IReadOnlyList<string> documentIds,
        string declineReason,
        CancellationToken cancellationToken = default)
    {
        var code = RequireId(signerAccessCode, "Signer access code");
        ArgumentNullException.ThrowIfNull(documentIds);
        if (documentIds.Count == 0)
            throw new ValidationException("At least one document ID is required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(declineReason);

        var path = AppendQueryString(
            "signers/documents/decline-multiple",
            AccessCodeQuery(code));

        return CallVoidAsync(
            path,
            HttpMethod.Put,
            new DeclineMultipleDocumentsRequest
            {
                DocumentIds = documentIds,
                DeclineReason = declineReason,
            },
            cancellationToken,
            authenticate: false);
    }

    /// <summary><c>GET /signers/{signer_id}/documents/{document_id}/download/{artifact_name}</c> — public download of a signer document artifact.</summary>
    /// <param name="signerId">Signer requesting the download.</param>
    /// <param name="documentId">Document to download.</param>
    /// <param name="signerAccessCode">Legacy parameter retained for compatibility; the current public endpoint does not use an access code.</param>
    /// <param name="artifactName">Which artifact to download; one of the <see cref="DocumentArtifactNames"/> values (<c>original</c>, <c>certificated</c>, <c>certificate-page</c>, <c>pades</c>, <c>bundle</c>). Defaults to <c>certificated</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<byte[]> DownloadAsync(
        string signerId,
        string documentId,
        string? signerAccessCode = null,
        string artifactName = DocumentArtifactNames.Certificated,
        CancellationToken cancellationToken = default)
    {
        var signer = RequireId(signerId, "Signer ID");
        var document = RequireId(documentId, "Document ID");
        var artifact = RequireId(artifactName, "Artifact name");

        var path = $"signers/{signer}/documents/{document}/download/{artifact}";

        return CallBinaryAsync(path, HttpMethod.Get, cancellationToken, authenticate: false);
    }

    private static Dictionary<string, string?> BuildPaginationQuery(SignerDocumentListParams? parameters)
    {
        var query = new Dictionary<string, string?>();
        if (parameters is null) return query;

        if (parameters.Page.HasValue) query["page"] = parameters.Page.Value.ToString();
        if (parameters.PerPage.HasValue) query["per-page"] = parameters.PerPage.Value.ToString();
        if (!string.IsNullOrWhiteSpace(parameters.Status)) query["status"] = parameters.Status;
        if (!string.IsNullOrWhiteSpace(parameters.Method)) query["method"] = parameters.Method;
        if (!string.IsNullOrWhiteSpace(parameters.Search)) query["search"] = parameters.Search;
        if (!string.IsNullOrWhiteSpace(parameters.Sort)) query["sort"] = parameters.Sort;

        return query;
    }

    private static Dictionary<string, string?> BuildSearchQuery(SignerDocumentListParams? parameters)
    {
        var query = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(parameters?.Search))
            query["search"] = parameters.Search;
        if (!string.IsNullOrWhiteSpace(parameters?.Status)) query["status"] = parameters.Status;
        if (!string.IsNullOrWhiteSpace(parameters?.Method)) query["method"] = parameters.Method;
        if (!string.IsNullOrWhiteSpace(parameters?.Sort)) query["sort"] = parameters.Sort;
        if (parameters?.Page.HasValue == true) query["page"] = parameters.Page.Value.ToString();
        if (parameters?.PerPage.HasValue == true) query["per-page"] = parameters.PerPage.Value.ToString();

        return query;
    }
}
