using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Signer-facing document access, signing, declining, certificate signing, and public downloads.</summary>
public sealed class SigningResource : BaseResource
{
    internal SigningResource(HttpClient http, Action<HttpRequestMessage>? authenticate = null)
        : base(http, authenticate: authenticate) { }

    /// <summary><c>GET /sign</c> — signer-facing endpoint: load the document and assignment data for the current signer access code.</summary>
    /// <param name="signerAccessCode">The signer's access code identifying which assignment to load.</param>
    /// <param name="hasAcceptedTerms">When set, records whether the signer has accepted the terms of use; sent as the <c>has_accepted_terms</c> query flag.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document and assignment details available to the signer.</returns>
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
    /// <returns>A task that completes when the assignment has been signed.</returns>
    public Task SignAsync(
        string documentId,
        string assignmentId,
        string signerAccessCode,
        IReadOnlyList<SignAssignmentValue> values,
        CancellationToken cancellationToken = default)
    {
        var document = PathSegment(documentId, "Document ID");
        var assignment = PathSegment(assignmentId, "Assignment ID");
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

    /// <summary>
    /// <c>POST /signers/certificate/start?signer-access-code={code}</c> — start an
    /// ICP-Brasil digital-certificate signature and return the Web PKI token to sign.
    /// This route is required for assignments whose verification method is
    /// <c>DigitalCertificate</c>. It is a production-only deployed extension that is not
    /// exposed by the sandbox or included in the published OpenAPI document.
    /// </summary>
    /// <param name="signerAccessCode">The signer's access code authorizing the signature.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Web PKI token required to create the digital signature.</returns>
    public Task<CertificateStartResult> StartCertificateAsync(
        string signerAccessCode,
        CancellationToken cancellationToken = default)
    {
        var code = RequireId(signerAccessCode, "Signer access code");
        var path = AppendQueryString("signers/certificate/start", AccessCodeQuery(code));

        return CallAsync<CertificateStartResult>(
            path,
            HttpMethod.Post,
            new Dictionary<string, object?> { [SignerAccessCodeParam] = code },
            cancellationToken: cancellationToken,
            authenticate: false);
    }

    /// <summary>
    /// <c>POST /signers/certificate/complete?signer-access-code={code}</c> — complete
    /// an ICP-Brasil digital-certificate signature using the signed Web PKI token returned by
    /// <see cref="StartCertificateAsync"/>. It is a production-only deployed extension that is
    /// not exposed by the sandbox or included in the published OpenAPI document.
    /// </summary>
    /// <param name="signerAccessCode">The signer's access code authorizing the signature.</param>
    /// <param name="token">The Web PKI token returned by <see cref="StartCertificateAsync"/> after it has been signed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The signer identity reported after the certificate signature is completed.</returns>
    public Task<CertificateCompleteResult> CompleteCertificateAsync(
        string signerAccessCode,
        string token,
        CancellationToken cancellationToken = default)
    {
        var code = RequireId(signerAccessCode, "Signer access code");
        var signedToken = RequireId(token, "Certificate token");
        var path = AppendQueryString("signers/certificate/complete", AccessCodeQuery(code));

        return CallAsync<CertificateCompleteResult>(
            path,
            HttpMethod.Post,
            new Dictionary<string, object?>
            {
                [SignerAccessCodeParam] = code,
                ["token"] = signedToken,
            },
            cancellationToken: cancellationToken,
            authenticate: false);
    }

    /// <summary><c>PUT /documents/{documentId}/assignments/{assignmentId}/reject?signer-access-code={code}</c> — signer-facing endpoint: decline an assignment with a reason.</summary>
    /// <param name="documentId">Document containing the assignment.</param>
    /// <param name="assignmentId">Assignment being declined.</param>
    /// <param name="signerAccessCode">The signer's access code authorizing the decline.</param>
    /// <param name="declineReason">Reason the signer is declining the assignment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the assignment has been declined.</returns>
    public Task DeclineAsync(
        string documentId,
        string assignmentId,
        string signerAccessCode,
        string declineReason,
        CancellationToken cancellationToken = default)
    {
        var document = PathSegment(documentId, "Document ID");
        var assignment = PathSegment(assignmentId, "Assignment ID");
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
    /// <returns>The signer's current document details.</returns>
    public Task<DocumentDetails> GetCurrentDocumentAsync(
        string signerId,
        string signerAccessCode,
        CancellationToken cancellationToken = default)
    {
        var signer = PathSegment(signerId, "Signer ID");
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
    /// <returns>A paginated collection of documents associated with the signer.</returns>
    public Task<PaginatedResult<DocumentListItem>> ListDocumentsAsync(
        string signerId,
        string signerAccessCode,
        SignerDocumentListParams? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var signer = PathSegment(signerId, "Signer ID");
        var code = RequireId(signerAccessCode, "Signer access code");

        var query = BuildDocumentQuery(parameters);
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
    /// <returns>A paginated collection of documents matching the search.</returns>
    public Task<PaginatedResult<DocumentListItem>> SearchDocumentsAsync(
        string signerId,
        string signerAccessCode,
        SignerDocumentListParams? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var signer = PathSegment(signerId, "Signer ID");
        var code = RequireId(signerAccessCode, "Signer access code");

        var query = BuildDocumentQuery(parameters);
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
    /// <returns>A task that completes when all requested documents have been signed.</returns>
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
    /// <returns>A task that completes when all requested documents have been declined.</returns>
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
    /// <param name="artifactName">Which artifact to download; one of the <see cref="DocumentArtifactNames"/> values (<c>original</c>, <c>certificated</c>, <c>certificate-page</c>, <c>pades</c>, <c>bundle</c>). Defaults to <c>certificated</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw document artifact bytes.</returns>
    public Task<byte[]> DownloadPublicAsync(
        string signerId,
        string documentId,
        string artifactName = DocumentArtifactNames.Certificated,
        CancellationToken cancellationToken = default)
    {
        var signer = PathSegment(signerId, "Signer ID");
        var document = PathSegment(documentId, "Document ID");
        var artifact = PathSegment(artifactName, "Artifact name");

        return CallBinaryAsync(
            $"signers/{signer}/documents/{document}/download/{artifact}",
            HttpMethod.Get,
            cancellationToken,
            authenticate: false);
    }

    /// <summary>Legacy public-download overload. The signer access code is ignored; this public endpoint does not use one.</summary>
    /// <param name="signerId">Signer requesting the download.</param>
    /// <param name="documentId">Document to download.</param>
    /// <param name="signerAccessCode">Ignored. Retained so existing call sites keep compiling; the current public endpoint takes no access code.</param>
    /// <param name="artifactName">Which artifact to download; one of the <see cref="DocumentArtifactNames"/> values (<c>original</c>, <c>certificated</c>, <c>certificate-page</c>, <c>pades</c>, <c>bundle</c>). Defaults to <c>certificated</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw document artifact bytes.</returns>
    [Obsolete("Use DownloadPublicAsync(signerId, documentId, artifactName, cancellationToken). The signerAccessCode argument is ignored.")]
    public Task<byte[]> DownloadAsync(
        string signerId,
        string documentId,
        string? signerAccessCode = null,
        string artifactName = DocumentArtifactNames.Certificated,
        CancellationToken cancellationToken = default)
    {
        return DownloadPublicAsync(signerId, documentId, artifactName, cancellationToken);
    }

    /// <summary>
    /// Build the shared signer-document query. Both the list and search routes accept the same keys;
    /// each documents a different subset and ignores the rest, so only values the caller set are sent.
    /// </summary>
    private static Dictionary<string, string?> BuildDocumentQuery(SignerDocumentListParams? parameters)
    {
        var query = new Dictionary<string, string?>();
        if (parameters is null) return query;

        if (!string.IsNullOrWhiteSpace(parameters.Search)) query["search"] = parameters.Search;
        if (!string.IsNullOrWhiteSpace(parameters.Status)) query["status"] = parameters.Status;
        if (!string.IsNullOrWhiteSpace(parameters.Method)) query["method"] = parameters.Method;
        if (!string.IsNullOrWhiteSpace(parameters.Sort)) query["sort"] = parameters.Sort;
        if (parameters.Page.HasValue) query["page"] = parameters.Page.Value.ToString();
        if (parameters.PerPage.HasValue) query["per-page"] = parameters.PerPage.Value.ToString();

        return query;
    }
}
