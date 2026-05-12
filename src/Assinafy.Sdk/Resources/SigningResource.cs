using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Signer-facing document access, signing, declining, and downloads.</summary>
public sealed class SigningResource : BaseResource
{
    internal SigningResource(HttpClient http) : base(http) { }

    /// <summary><c>GET /sign</c> — signer-facing endpoint: load the document and assignment data for the current signer access code.</summary>
    public Task<DocumentDetails> GetAsync(
        string signerAccessCode,
        bool? hasAcceptedTerms = null,
        CancellationToken cancellationToken = default)
    {
        var code = RequireId(signerAccessCode, "Signer access code");
        var query = new Dictionary<string, string?>
        {
            ["signer-access-code"] = code,
        };

        if (hasAcceptedTerms.HasValue)
            query["has_accepted_terms"] = hasAcceptedTerms.Value ? "true" : "false";

        return CallAsync<DocumentDetails>(
            AppendQueryString("sign", query),
            HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// <c>POST /documents/{documentId}/assignments/{assignmentId}?signer-access-code={code}</c>
    /// — submit a signer's field values. For virtual assignments the signer must call
    /// <see cref="SignerResource.ConfirmDataAsync"/> first; otherwise the API returns 400.
    /// The body uses camelCase keys (<c>itemId</c>, <c>fieldId</c>, <c>pageId</c>, <c>value</c>)
    /// per the Assinafy docs, which <see cref="SignAssignmentValue"/> applies automatically.
    /// </summary>
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
            new Dictionary<string, string?> { ["signer-access-code"] = code });

        return CallVoidAsync(path, HttpMethod.Post, values, cancellationToken);
    }

    /// <summary><c>PUT /documents/{documentId}/assignments/{assignmentId}/reject?signer-access-code={code}</c> — signer-facing endpoint: decline an assignment with a reason.</summary>
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
            new Dictionary<string, string?> { ["signer-access-code"] = code });

        return CallVoidAsync(
            path,
            HttpMethod.Put,
            new DeclineAssignmentRequest { DeclineReason = declineReason },
            cancellationToken);
    }

    /// <summary><c>GET /signers/{signer_id}/document?signer-access-code={code}</c> — fetch the signer's current document.</summary>
    public Task<DocumentDetails> GetCurrentDocumentAsync(
        string signerId,
        string signerAccessCode,
        CancellationToken cancellationToken = default)
    {
        var signer = RequireId(signerId, "Signer ID");
        var code = RequireId(signerAccessCode, "Signer access code");

        var path = AppendQueryString(
            $"signers/{signer}/document",
            new Dictionary<string, string?> { ["signer-access-code"] = code });

        return CallAsync<DocumentDetails>(path, HttpMethod.Get, cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /signers/{signer_id}/documents?signer-access-code={code}</c> — list all documents associated with the signer.</summary>
    public Task<PaginatedResult<DocumentListItem>> ListDocumentsAsync(
        string signerId,
        string signerAccessCode,
        SignerDocumentListParams? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var signer = RequireId(signerId, "Signer ID");
        var code = RequireId(signerAccessCode, "Signer access code");

        var query = BuildListQuery(parameters);
        query["signer-access-code"] = code;

        return CallListAsync<DocumentListItem>(
            $"signers/{signer}/documents",
            query,
            cancellationToken);
    }

    /// <summary><c>PUT /signers/documents/sign-multiple?signer-access-code={code}</c> — sign multiple virtual-method documents in one request.</summary>
    public Task SignMultipleAsync(
        string signerAccessCode,
        IReadOnlyList<string> documentIds,
        CancellationToken cancellationToken = default)
    {
        var code = RequireId(signerAccessCode, "Signer access code");
        ArgumentNullException.ThrowIfNull(documentIds);

        var path = AppendQueryString(
            "signers/documents/sign-multiple",
            new Dictionary<string, string?> { ["signer-access-code"] = code });

        return CallVoidAsync(
            path,
            HttpMethod.Put,
            new SignMultipleDocumentsRequest { DocumentIds = documentIds },
            cancellationToken);
    }

    /// <summary><c>PUT /signers/documents/decline-multiple?signer-access-code={code}</c> — decline multiple documents at once with a single reason.</summary>
    public Task DeclineMultipleAsync(
        string signerAccessCode,
        IReadOnlyList<string> documentIds,
        string declineReason,
        CancellationToken cancellationToken = default)
    {
        var code = RequireId(signerAccessCode, "Signer access code");
        ArgumentNullException.ThrowIfNull(documentIds);
        ArgumentException.ThrowIfNullOrWhiteSpace(declineReason);

        var path = AppendQueryString(
            "signers/documents/decline-multiple",
            new Dictionary<string, string?> { ["signer-access-code"] = code });

        return CallVoidAsync(
            path,
            HttpMethod.Put,
            new DeclineMultipleDocumentsRequest
            {
                DocumentIds = documentIds,
                DeclineReason = declineReason,
            },
            cancellationToken);
    }

    /// <summary><c>GET /signers/{signer_id}/documents/{document_id}/download/{artifact_name}?signer-access-code={code}</c> — signer-facing download of a document artifact.</summary>
    public Task<byte[]> DownloadAsync(
        string signerId,
        string documentId,
        string signerAccessCode,
        string artifactName = DocumentArtifactNames.Certificated,
        CancellationToken cancellationToken = default)
    {
        var signer = RequireId(signerId, "Signer ID");
        var document = RequireId(documentId, "Document ID");
        var code = RequireId(signerAccessCode, "Signer access code");
        var artifact = RequireId(artifactName, "Artifact name");

        var path = AppendQueryString(
            $"signers/{signer}/documents/{document}/download/{artifact}",
            new Dictionary<string, string?> { ["signer-access-code"] = code });

        return CallBinaryAsync(path, HttpMethod.Get, cancellationToken);
    }

    private static Dictionary<string, string?> BuildListQuery(SignerDocumentListParams? parameters)
    {
        var query = new Dictionary<string, string?>();
        if (parameters is null) return query;

        if (!string.IsNullOrWhiteSpace(parameters.Status)) query["status"] = parameters.Status;
        if (!string.IsNullOrWhiteSpace(parameters.Method)) query["method"] = parameters.Method;
        if (!string.IsNullOrWhiteSpace(parameters.Search)) query["search"] = parameters.Search;
        if (!string.IsNullOrWhiteSpace(parameters.Sort)) query["sort"] = parameters.Sort;
        if (parameters.Page.HasValue) query["page"] = parameters.Page.Value.ToString();
        if (parameters.PerPage.HasValue) query["per-page"] = parameters.PerPage.Value.ToString();

        return query;
    }
}
