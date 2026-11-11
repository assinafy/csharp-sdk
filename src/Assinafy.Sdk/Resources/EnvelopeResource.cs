using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Workspace envelopes: send and manage several documents together while each keeps its own signing lifecycle.</summary>
public sealed class EnvelopeResource : BaseResource
{
    internal EnvelopeResource(HttpClient http, string? defaultAccountId = null, Action<HttpRequestMessage>? authenticate = null)
        : base(http, defaultAccountId, authenticate) { }

    /// <summary><c>GET /accounts/{accountId}/envelopes</c> — list envelopes newest first, with document statuses and signers.</summary>
    /// <param name="queryParams">Optional search, status (open or closed), page and per-page filters.</param>
    /// <param name="accountId">Workspace; falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The requested page of envelopes.</returns>
    public Task<PaginatedResult<DocumentEnvelope>> ListAsync(IDictionary<string, string?>? queryParams = null,
        string? accountId = null, CancellationToken cancellationToken = default) =>
        CallListAsync<DocumentEnvelope>($"accounts/{AccountId(accountId)}/envelopes", queryParams, cancellationToken);

    /// <summary><c>POST /accounts/{accountId}/envelopes</c> — send processed documents to shared signers. Requires the Envelopes plan feature.</summary>
    /// <remarks>Consumes one document allowance, notification/identity costs once per signer, and certificate costs per document. Notification failures are recorded without undoing creation. A credit ceiling refusal creates and charges nothing. No SDK retries.</remarks>
    /// <param name="request">Envelope name, signers and documents, with optional message and credit ceiling.</param>
    /// <param name="idempotencyKey">Optional case-sensitive workspace key. Reusing it returns the original envelope regardless of body; 409 IdempotencyKeyInUse means the first call is still running.</param>
    /// <param name="accountId">Workspace; falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created or replayed envelope, including signing links.</returns>
    public Task<DocumentEnvelope> CreateAsync(CreateEnvelopeRequest request, string? idempotencyKey = null,
        string? accountId = null, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        if (request.MaxTotalCredits < 0) throw new ValidationException("Credit ceiling must not be negative.");
        return CallAsync<DocumentEnvelope>($"accounts/{AccountId(accountId)}/envelopes", HttpMethod.Post, request,
            cancellationToken: cancellationToken,
            requestHeaders: IdempotencyHeaders(idempotencyKey));
    }

    /// <summary><c>POST /accounts/{accountId}/envelopes/estimate-cost</c> — validate and price an envelope without creating or charging anything.</summary>
    /// <param name="request">Creation body; the API ignores MaxTotalCredits when estimating.</param>
    /// <param name="accountId">Workspace; falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Document/credit costs, balances, and any blocking reason.</returns>
    public Task<AssignmentCostEstimate> EstimateCostAsync(CreateEnvelopeRequest request,
        string? accountId = null, CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        return CallAsync<AssignmentCostEstimate>($"accounts/{AccountId(accountId)}/envelopes/estimate-cost",
            HttpMethod.Post, request, cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /accounts/{accountId}/envelopes/{envelopeId}</c> — get an envelope and all its documents in order.</summary>
    /// <param name="envelopeId">Envelope identifier.</param>
    /// <param name="accountId">Workspace; falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The envelope and its current signing links.</returns>
    public Task<DocumentEnvelope> GetAsync(string envelopeId, string? accountId = null, CancellationToken cancellationToken = default) =>
        CallAsync<DocumentEnvelope>(EnvelopePath(envelopeId, accountId), HttpMethod.Get, cancellationToken: cancellationToken);

    /// <summary><c>PATCH /accounts/{accountId}/envelopes/{envelopeId}</c> — change name, message or document order without sending notifications.</summary>
    /// <param name="envelopeId">Envelope identifier.</param>
    /// <param name="changes">Nonempty snake_case map: name, message (null clears), document_ids (every document exactly once in the new order).</param>
    /// <param name="accountId">Workspace; falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated envelope. Closed envelopes can also be updated.</returns>
    public Task<DocumentEnvelope> UpdateAsync(string envelopeId, IDictionary<string, object?> changes,
        string? accountId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(changes);
        if (changes.Count == 0) throw new ValidationException("At least one envelope attribute is required.");
        return CallAsync<DocumentEnvelope>(EnvelopePath(envelopeId, accountId), HttpMethod.Patch, changes,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>DELETE /accounts/{accountId}/envelopes/{envelopeId}</c> — delete a closed envelope, leaving its documents intact. Open envelopes return 409 EnvelopeOpen.</summary>
    /// <param name="envelopeId">Envelope identifier.</param>
    /// <param name="accountId">Workspace; falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task completing after deletion.</returns>
    public Task DeleteAsync(string envelopeId, string? accountId = null, CancellationToken cancellationToken = default) =>
        CallVoidAsync(EnvelopePath(envelopeId, accountId), HttpMethod.Delete, cancellationToken: cancellationToken);

    /// <summary><c>GET /accounts/{accountId}/envelopes/{envelopeId}/activities</c> — get the trail of current and removed documents, excluding deleted documents.</summary>
    /// <param name="envelopeId">Envelope identifier.</param>
    /// <param name="queryParams">Optional sort (-id newest first, or id), page and per-page.</param>
    /// <param name="accountId">Workspace; falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A page of activities, each naming its document.</returns>
    public Task<PaginatedResult<EnvelopeActivity>> ActivitiesAsync(string envelopeId,
        IDictionary<string, string?>? queryParams = null, string? accountId = null, CancellationToken cancellationToken = default) =>
        CallListAsync<EnvelopeActivity>($"{EnvelopePath(envelopeId, accountId)}/activities", queryParams, cancellationToken);

    /// <summary><c>GET /accounts/{accountId}/envelopes/{envelopeId}/download/{artifactName}</c> — download a ZIP of the chosen artifact, in document order.</summary>
    /// <param name="envelopeId">Envelope identifier.</param>
    /// <param name="artifactName">original, certificated, certificate-page, pades or bundle. Signed artifacts include only certified documents; 404 when none has the artifact.</param>
    /// <param name="accountId">Workspace; falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>ZIP bytes; bundle places each document's bundle files in its own folder.</returns>
    public Task<byte[]> DownloadAsync(string envelopeId, string artifactName = DocumentArtifactNames.Certificated,
        string? accountId = null, CancellationToken cancellationToken = default) =>
        CallBinaryAsync($"{EnvelopePath(envelopeId, accountId)}/download/{PathSegment(artifactName, "Artifact name")}",
            HttpMethod.Get, cancellationToken);

    /// <summary><c>PUT /accounts/{accountId}/envelopes/{envelopeId}/cancel</c> — cancel cancellable documents and notify their owners, signers and copy receivers.</summary>
    /// <param name="envelopeId">Envelope identifier.</param>
    /// <param name="accountId">Workspace; falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Every document's status and whether this call cancelled it. Closed or certifying documents are left unchanged.</returns>
    public Task<IReadOnlyList<EnvelopeCancellationResult>> CancelAsync(string envelopeId,
        string? accountId = null, CancellationToken cancellationToken = default) =>
        CallListBodyAsync<EnvelopeCancellationResult>($"{EnvelopePath(envelopeId, accountId)}/cancel",
            HttpMethod.Put, cancellationToken: cancellationToken);

    /// <summary><c>PUT /accounts/{accountId}/envelopes/{envelopeId}/signers/{signerId}/resend</c> — send one reminder for all documents still awaiting this signer; paid channels are charged once.</summary>
    /// <param name="envelopeId">Envelope identifier.</param>
    /// <param name="signerId">An envelope signer.</param>
    /// <param name="accountId">Workspace; falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether every notification method sent the reminder.</returns>
    public Task<EnvelopeResendResult> ResendNotificationAsync(string envelopeId, string signerId,
        string? accountId = null, CancellationToken cancellationToken = default) =>
        CallAsync<EnvelopeResendResult>($"{EnvelopePath(envelopeId, accountId)}/signers/{PathSegment(signerId, "Signer ID")}/resend",
            HttpMethod.Put, cancellationToken: cancellationToken);

    /// <summary><c>POST /accounts/{accountId}/envelopes/{envelopeId}/documents</c> — append a processed document to an open envelope for existing signers and send their updated invitations.</summary>
    /// <param name="envelopeId">Envelope identifier; at most twenty documents are allowed.</param>
    /// <param name="request">Document and existing envelope signers. Only certificate signatures incur new costs.</param>
    /// <param name="maxTotalCredits">Optional nonnegative credit ceiling; refusal returns 409 without creating or charging.</param>
    /// <param name="accountId">Workspace; falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The envelope with the appended document.</returns>
    public Task<DocumentEnvelope> AddDocumentAsync(string envelopeId, EnvelopeDocumentRequest request,
        decimal? maxTotalCredits = null, string? accountId = null, CancellationToken cancellationToken = default)
    {
        ValidateDocument(request);
        if (maxTotalCredits < 0) throw new ValidationException("Credit ceiling must not be negative.");
        return CallAsync<DocumentEnvelope>($"{EnvelopePath(envelopeId, accountId)}/documents", HttpMethod.Post,
            new { request.Id, request.SignerIds, request.Method, request.Entries, request.ExpiresAt, request.CopyReceiverIds, MaxTotalCredits = maxTotalCredits },
            cancellationToken: cancellationToken);
    }

    /// <summary><c>DELETE /accounts/{accountId}/envelopes/{envelopeId}/documents/{documentId}</c> — remove and cancel an open document, notifying its parties; the other documents carry on.</summary>
    /// <param name="envelopeId">Envelope identifier.</param>
    /// <param name="documentId">Open member document; an already removed document returns 404.</param>
    /// <param name="accountId">Workspace; falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The envelope without the removed document.</returns>
    public Task<DocumentEnvelope> RemoveDocumentAsync(string envelopeId, string documentId,
        string? accountId = null, CancellationToken cancellationToken = default) =>
        CallAsync<DocumentEnvelope>($"{EnvelopePath(envelopeId, accountId)}/documents/{PathSegment(documentId, "Document ID")}",
            HttpMethod.Delete, cancellationToken: cancellationToken);

    private string EnvelopePath(string envelopeId, string? accountId) =>
        $"accounts/{AccountId(accountId)}/envelopes/{PathSegment(envelopeId, "Envelope ID")}";

    private static void ValidateRequest(CreateEnvelopeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);
        if (request.Signers is not { Count: >= 1 and <= 20 } || request.Documents is not { Count: >= 1 and <= 20 })
            throw new ValidationException("An envelope requires one to twenty signers and documents.");
        foreach (var signer in request.Signers)
            if (signer is null || string.IsNullOrWhiteSpace(signer.Id))
                throw new ValidationException("An envelope signer ID is required.");
        foreach (var document in request.Documents) ValidateDocument(document);
    }

    private static void ValidateDocument(EnvelopeDocumentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Id) || request.SignerIds is not { Count: > 0 } || request.SignerIds.Any(string.IsNullOrWhiteSpace))
            throw new ValidationException("An envelope document ID and signer IDs are required.");
    }
}
