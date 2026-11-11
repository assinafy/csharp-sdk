using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Signer-facing document access, signing, declining, certificate signing, and public downloads.</summary>
public sealed class SigningResource : BaseResource
{
    internal SigningResource(HttpClient http, Action<HttpRequestMessage>? authenticate = null)
        : base(http, authenticate: authenticate) { }

    /// <summary><c>GET /signers/identity-verification?signer-access-code={code}</c> — get the current biometric verification state, backfilling a pending provider decision. Sends no workspace credential.</summary>
    /// <param name="signerAccessCode">Required signer access code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether capture can start, status, capture URL, expiration, and completion time. InReview permits signing but holds certification until approved.</returns>
    public Task<IdentityVerificationStatus> GetIdentityVerificationAsync(
        string signerAccessCode,
        CancellationToken cancellationToken = default)
    {
        var code = RequireId(signerAccessCode, "Signer access code");
        return CallAsync<IdentityVerificationStatus>(
            AppendQueryString("signers/identity-verification", AccessCodeQuery(code)),
            HttpMethod.Get,
            cancellationToken: cancellationToken,
            authenticate: false);
    }

    /// <summary><c>POST /signers/identity-verification?signer-access-code={code}</c> — record explicit biometric-processing consent and open a hosted capture session. Sends no workspace credential.</summary>
    /// <remarks>Only a missing or Expired session may be started. Pending, InReview, Approved, Declined, and Abandoned return 400. Restarting an Expired session adds no charge; Declined or Abandoned require a new document and assignment.</remarks>
    /// <param name="signerAccessCode">Required signer access code.</param>
    /// <param name="consent">The signer's explicit consent to biometric processing; must be true.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The session ID, capture URL, and optional expiration. Open the bearer URL as-is, including its fragment; never log or persist it.</returns>
    public Task<IdentityVerificationSession> StartIdentityVerificationAsync(
        string signerAccessCode,
        bool consent,
        CancellationToken cancellationToken = default)
    {
        var code = RequireId(signerAccessCode, "Signer access code");
        if (!consent)
            throw new ValidationException("Explicit consent to biometric processing is required.");

        return CallAsync<IdentityVerificationSession>(
            AppendQueryString("signers/identity-verification", AccessCodeQuery(code)),
            HttpMethod.Post,
            new { consent },
            cancellationToken: cancellationToken,
            authenticate: false);
    }

    /// <summary><c>GET /sign</c> — signer-facing endpoint: load the document and assignment data for the current signer access code.</summary>
    /// <remarks>Returns 409 while the document is being prepared. Digital-certificate signers must confirm their data and accept terms before this call; the query flag does not bypass that requirement.</remarks>
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

    /// <summary><c>GET /sign</c> — load a signer's document with optional envelope expansion or navigation. Closed codes return a limited payload and certified-copy availability.</summary>
    /// <param name="signerAccessCode">Required signer credential; workspace credentials are never sent.</param>
    /// <param name="parameters">Terms flag, envelope expansion, and optional envelope document to open.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document and the current signer's state.</returns>
    public Task<DocumentDetails> GetWithOptionsAsync(string signerAccessCode, SigningDocumentParams parameters, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var query = AccessCodeQuery(RequireId(signerAccessCode, "Signer access code"));
        if (parameters.HasAcceptedTerms.HasValue) query["has_accepted_terms"] = parameters.HasAcceptedTerms.Value ? "true" : "false";
        if (parameters.ExpandEnvelope) query["expand"] = "envelope";
        if (parameters.DocumentId is not null) query["document_id"] = RequireId(parameters.DocumentId, "Document ID");
        return CallAsync<DocumentDetails>(AppendQueryString("sign", query), HttpMethod.Get,
            cancellationToken: cancellationToken, authenticate: false);
    }

    /// <summary><c>GET /signers/{signerId}/document?expand=envelope</c> — read the current document and its envelope before certificate or identity-check data confirmation.</summary>
    /// <param name="signerId">Signer whose current document to read.</param>
    /// <param name="signerAccessCode">Required signer access code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document, current signer, and envelope summary.</returns>
    public Task<DocumentDetails> GetCurrentDocumentWithEnvelopeAsync(string signerId, string signerAccessCode, CancellationToken cancellationToken = default)
    {
        var query = AccessCodeQuery(RequireId(signerAccessCode, "Signer access code"));
        query["expand"] = "envelope";
        return CallAsync<DocumentDetails>(AppendQueryString($"signers/{PathSegment(signerId, "Signer ID")}/document", query),
            HttpMethod.Get, cancellationToken: cancellationToken, authenticate: false);
    }

    /// <summary><c>GET /sign/theme</c> — read the sender's branding before verification or after document deletion. Logo URLs require the signer access code.</summary>
    /// <param name="signerAccessCode">Required signer access code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The account name, colors, and optional logo URL.</returns>
    public Task<AccountTheme> GetThemeAsync(string signerAccessCode, CancellationToken cancellationToken = default) =>
        CallAsync<AccountTheme>(AppendQueryString("sign/theme", AccessCodeQuery(RequireId(signerAccessCode, "Signer access code"))),
            HttpMethod.Get, cancellationToken: cancellationToken, authenticate: false);

    /// <summary><c>GET /sign/logo</c> — download the sender's logo before verification or after document deletion.</summary>
    /// <param name="signerAccessCode">Required signer access code.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The logo image bytes.</returns>
    public Task<byte[]> DownloadLogoAsync(string signerAccessCode, CancellationToken cancellationToken = default) =>
        CallBinaryAsync(AppendQueryString("sign/logo", AccessCodeQuery(RequireId(signerAccessCode, "Signer access code"))),
            HttpMethod.Get, cancellationToken, authenticate: false);

    /// <summary><c>POST /sign/certified-copy</c> — send a new 30-minute download link to the original signing contact. Works after signer access closes.</summary>
    /// <remarks>Email permits one resend per two minutes and ten per day; WhatsApp permits one resend per signature request. The SDK does not retry.</remarks>
    /// <param name="signerAccessCode">Required signer access code.</param>
    /// <param name="documentId">Optional other document in the code's envelope that this signer is on.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Remaining availability and the masked destination.</returns>
    public Task<CertifiedCopyStatus> SendCertifiedCopyAsync(string signerAccessCode, string? documentId = null, CancellationToken cancellationToken = default)
    {
        if (documentId is not null) RequireId(documentId, "Document ID");
        return CallAsync<CertifiedCopyStatus>(AppendQueryString("sign/certified-copy", AccessCodeQuery(RequireId(signerAccessCode, "Signer access code"))),
            HttpMethod.Post, documentId is null ? null : new { document_id = documentId }, cancellationToken: cancellationToken, authenticate: false);
    }

    /// <summary><c>GET /signers/downloads/{token}[/pades]</c> — download a certified copy using its sent-link token. Expired or unknown tokens return 302, surfaced as an API exception; no redirect is followed.</summary>
    /// <param name="token">Token from the completion or resend link. Never log it.</param>
    /// <param name="pades">Request the PAdES version; 404 when the document has none.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The certified PDF bytes.</returns>
    public Task<byte[]> DownloadCertifiedCopyAsync(string token, bool pades = false, CancellationToken cancellationToken = default) =>
        CallBinaryAsync($"signers/downloads/{PathSegment(token, "Download token")}{(pades ? "/pades" : "")}",
            HttpMethod.Get, cancellationToken, authenticate: false);

    /// <summary><c>POST /signers/downloads/{token}/resend</c> — request a new certified-copy link from an expired link, subject to the same channel and resend limits. Copy receivers return 409.</summary>
    /// <param name="token">Known download-link token, including expired tokens.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Remaining availability and the masked destination.</returns>
    public Task<CertifiedCopyStatus> ResendCertifiedCopyAsync(string token, CancellationToken cancellationToken = default) =>
        CallAsync<CertifiedCopyStatus>($"signers/downloads/{PathSegment(token, "Download token")}/resend", HttpMethod.Post,
            cancellationToken: cancellationToken, authenticate: false);

    /// <summary><c>GET /signers/{signerId}/documents/{documentId}/download/original</c> — download an original document with its signer access code. Closed codes return 410; certified artifacts use sent-link tokens instead.</summary>
    /// <param name="signerId">Signer authorized by the access code.</param>
    /// <param name="documentId">Document reached by the access code.</param>
    /// <param name="signerAccessCode">Required signer credential.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The original document bytes.</returns>
    public Task<byte[]> DownloadOriginalAsync(string signerId, string documentId, string signerAccessCode, CancellationToken cancellationToken = default) =>
        CallBinaryAsync(AppendQueryString($"signers/{PathSegment(signerId, "Signer ID")}/documents/{PathSegment(documentId, "Document ID")}/download/original",
            AccessCodeQuery(RequireId(signerAccessCode, "Signer access code"))), HttpMethod.Get, cancellationToken, authenticate: false);

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

    /// <summary><c>GET /signers/{signer_id}/documents?signer-access-code={code}</c> — list all documents associated with the signer, authenticating with the signer access code only.</summary>
    /// <remarks>
    /// Workspace credentials are never sent. A null or blank access code is rejected rather than
    /// falling back to workspace access; use <see cref="ListDocumentsAsWorkspaceAsync"/> for that.
    /// Response <c>data</c>: an array of <see cref="DocumentListItem"/>, with <c>X-Pagination-*</c> headers in <see cref="PaginatedResult{T}.Meta"/>.
    /// </remarks>
    /// <param name="signerId">Signer whose documents to list.</param>
    /// <param name="signerAccessCode">Required. The signer's access code.</param>
    /// <param name="parameters">Optional pagination. The documented endpoint accepts <c>Page</c> and <c>PerPage</c>; explicitly set legacy filters are retained for compatibility.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated collection of documents associated with the signer.</returns>
    public Task<PaginatedResult<DocumentListItem>> ListDocumentsAsync(
        string signerId,
        string signerAccessCode,
        SignerDocumentListParams? parameters = null,
        CancellationToken cancellationToken = default) =>
        SignerDocumentsAsync(signerId, "documents", RequireId(signerAccessCode, "Signer access code"), parameters, cancellationToken);

    /// <summary>
    /// <c>GET /signers/{signer_id}/documents</c> — list a signer's documents with the client's
    /// workspace credentials (API key or bearer token). The signer must belong to that workspace.
    /// No signer access code is sent.
    /// </summary>
    /// <remarks>Response <c>data</c>: an array of <see cref="DocumentListItem"/>, with <c>X-Pagination-*</c> headers in <see cref="PaginatedResult{T}.Meta"/>.</remarks>
    /// <param name="signerId">Signer whose documents to list.</param>
    /// <param name="parameters">Optional pagination: <c>Page</c> and <c>PerPage</c> (max 100).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated collection of documents associated with the signer.</returns>
    public Task<PaginatedResult<DocumentListItem>> ListDocumentsAsWorkspaceAsync(
        string signerId,
        SignerDocumentListParams? parameters = null,
        CancellationToken cancellationToken = default) =>
        SignerDocumentsAsync(signerId, "documents", null, parameters, cancellationToken);

    /// <summary>
    /// <c>GET /signers/{signer_id}/documents/search?signer-access-code={code}</c> — search the signer's
    /// documents by name, returning a compact representation. Use <see cref="ListDocumentsAsync"/> when you
    /// need the full document shape or pagination.
    /// </summary>
    /// <remarks>
    /// Workspace credentials are never sent. A null or blank access code is rejected rather than
    /// falling back to workspace access; use <see cref="SearchDocumentsAsWorkspaceAsync"/> for that.
    /// </remarks>
    /// <param name="signerId">Signer whose documents to search.</param>
    /// <param name="signerAccessCode">Required. The signer's access code.</param>
    /// <param name="parameters">Optional search term. The documented endpoint accepts <c>Search</c>; explicitly set legacy filters and pagination are retained for compatibility.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated collection of documents matching the search.</returns>
    public Task<PaginatedResult<DocumentListItem>> SearchDocumentsAsync(
        string signerId,
        string signerAccessCode,
        SignerDocumentListParams? parameters = null,
        CancellationToken cancellationToken = default) =>
        SignerDocumentsAsync(signerId, "documents/search", RequireId(signerAccessCode, "Signer access code"), parameters, cancellationToken);

    /// <summary>
    /// <c>GET /signers/{signer_id}/documents/search</c> — search a signer's documents with the client's
    /// workspace credentials (API key or bearer token). The signer must belong to that workspace.
    /// No signer access code is sent.
    /// </summary>
    /// <remarks>Response <c>data</c>: an array of compact <see cref="DocumentListItem"/> entries.</remarks>
    /// <param name="signerId">Signer whose documents to search.</param>
    /// <param name="parameters">Optional <c>Search</c> term.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated collection of documents matching the search.</returns>
    public Task<PaginatedResult<DocumentListItem>> SearchDocumentsAsWorkspaceAsync(
        string signerId,
        SignerDocumentListParams? parameters = null,
        CancellationToken cancellationToken = default) =>
        SignerDocumentsAsync(signerId, "documents/search", null, parameters, cancellationToken);

    /// <summary>
    /// Shared signer-document list/search call. With an access code the request carries only that
    /// code; without one it carries the workspace credential. Public callers decide which explicitly.
    /// </summary>
    private Task<PaginatedResult<DocumentListItem>> SignerDocumentsAsync(
        string signerId,
        string route,
        string? accessCode,
        SignerDocumentListParams? parameters,
        CancellationToken cancellationToken)
    {
        var signer = PathSegment(signerId, "Signer ID");
        var query = BuildDocumentQuery(parameters);
        if (accessCode is not null)
            query[SignerAccessCodeParam] = accessCode;

        return CallListAsync<DocumentListItem>(
            $"signers/{signer}/{route}",
            query,
            cancellationToken,
            authenticate: accessCode is null);
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

    /// <summary><c>GET /signers/{signer_id}/documents/{document_id}/download/{artifact_name}</c> — legacy link without a signer code. Production returns a 302 expired-link response. Use DownloadOriginalAsync or DownloadCertifiedCopyAsync for downloads.</summary>
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

    /// <summary>Legacy download overload. A supplied signer code authorizes only the original artifact; without a code, production returns a 302 expired-link response. Use DownloadOriginalAsync or DownloadCertifiedCopyAsync.</summary>
    /// <param name="signerId">Signer requesting the download.</param>
    /// <param name="documentId">Document to download.</param>
    /// <param name="signerAccessCode">Signer credential for an original artifact; null retains the legacy unauthenticated request.</param>
    /// <param name="artifactName">Which artifact to download; one of the <see cref="DocumentArtifactNames"/> values (<c>original</c>, <c>certificated</c>, <c>certificate-page</c>, <c>pades</c>, <c>bundle</c>). Defaults to <c>certificated</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The raw document artifact bytes.</returns>
    [Obsolete("Use DownloadOriginalAsync with a signer access code, or DownloadCertifiedCopyAsync with a sent-link token.")]
    public Task<byte[]> DownloadAsync(
        string signerId,
        string documentId,
        string? signerAccessCode = null,
        string artifactName = DocumentArtifactNames.Certificated,
        CancellationToken cancellationToken = default)
    {
        if (signerAccessCode is null)
            return DownloadPublicAsync(signerId, documentId, artifactName, cancellationToken);

        return CallBinaryAsync(AppendQueryString(
            $"signers/{PathSegment(signerId, "Signer ID")}/documents/{PathSegment(documentId, "Document ID")}/download/{PathSegment(artifactName, "Artifact name")}",
            AccessCodeQuery(RequireId(signerAccessCode, "Signer access code"))), HttpMethod.Get, cancellationToken, authenticate: false);
    }

    /// <summary>
    /// Build the shared signer-document query. Both the list and search routes accept the same keys;
    /// each documents a different subset and ignores the rest, so only values the caller set are sent.
    /// </summary>
    private static Dictionary<string, string?> BuildDocumentQuery(SignerDocumentListParams? parameters)
    {
        var query = new Dictionary<string, string?>();
        if (parameters is null) return query;

        if (parameters.EnvelopeId is not null) query["envelope_id"] = RequireId(parameters.EnvelopeId, "Envelope ID");
        if (!string.IsNullOrWhiteSpace(parameters.Search)) query["search"] = parameters.Search;
        if (!string.IsNullOrWhiteSpace(parameters.Status)) query["status"] = parameters.Status;
        if (!string.IsNullOrWhiteSpace(parameters.Method)) query["method"] = parameters.Method;
        if (!string.IsNullOrWhiteSpace(parameters.Sort)) query["sort"] = parameters.Sort;
        if (parameters.Page.HasValue) query["page"] = parameters.Page.Value.ToString();
        if (parameters.PerPage.HasValue) query["per-page"] = parameters.PerPage.Value.ToString();

        return query;
    }
}
