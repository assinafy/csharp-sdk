using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>
/// Contract lifecycle management for workspace documents. Reads require <c>documents:read</c>;
/// writes require <c>documents:write</c> and a plan with contract tracking. Tracking never sends
/// a document for signature. Upload the document first, then set its terms.
/// </summary>
public sealed class ContractResource : BaseResource
{
    internal ContractResource(HttpClient http, string? defaultAccountId = null, Action<HttpRequestMessage>? authenticate = null)
        : base(http, defaultAccountId, authenticate) { }

    /// <summary><c>GET /accounts/{accountId}/contracts</c> — list tracked contracts, including after the plan lapses.</summary>
    /// <param name="queryParams">Optional filters: comma-separated <c>status</c>, <c>counterparty</c>, <c>search</c>,
    /// <c>end_date_from</c>/<c>end_date_to</c> (YYYY-MM-DD), <c>ending_within_days</c> (0–3650, Brasília calendar),
    /// <c>external_system</c>/<c>external_id</c>, <c>sort</c> (±end_date or ±updated_at, default -updated_at),
    /// <c>expand=document</c>, <c>page</c>, and <c>per-page</c>.</param>
    /// <param name="accountId">Workspace; falls back to the client's default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A page of contracts, with documents when expanded.</returns>
    public Task<PaginatedResult<DocumentContract>> ListAsync(
        IDictionary<string, string?>? queryParams = null,
        string? accountId = null,
        CancellationToken cancellationToken = default) =>
        CallListAsync<DocumentContract>($"accounts/{AccountId(accountId)}/contracts", queryParams, cancellationToken);

    /// <summary><c>GET /accounts/{accountId}/documents/{documentId}/contract</c> — read terms or an amendment; 404 when absent.</summary>
    /// <param name="documentId">Document whose terms or amendment link to read.</param>
    /// <param name="accountId">Workspace; falls back to the client's default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="DocumentContract"/> with amendments, or a <see cref="DocumentContractAmendment"/>.</returns>
    public Task<ContractRecord> GetAsync(string documentId, string? accountId = null, CancellationToken cancellationToken = default) =>
        CallAsync<ContractRecord>(ContractPath(documentId, accountId), HttpMethod.Get, cancellationToken: cancellationToken);

    /// <summary>
    /// <c>PUT /accounts/{accountId}/documents/{documentId}/contract</c> — set only the supplied terms.
    /// Omitted keys stay unchanged; explicit null clears a term, except auto_renew resets to false,
    /// currency resets to BRL while a value exists, and status rejects null. Metadata and external references replace
    /// their whole collections. With <c>parent_document_id</c> and <c>relation_type</c>, link an amendment,
    /// renewal, or termination instrument instead. A pending amendment's null removes a delta key;
    /// <c>parent_document_id: null</c> unlinks it. Applied amendments are read-only. Send the read
    /// <c>updated_at</c> to reject stale writes with 409. Cancelled, declined, or expired documents refuse
    /// new terms/links; already-certificated documents accept them.
    /// </summary>
    /// <param name="documentId">Existing document to track or link.</param>
    /// <param name="terms">Nonempty JSON-object attributes using API snake_case names. Supported keys:
    /// <c>status</c>, <c>counterparty_name</c>, <c>counterparty_taxpayer_id</c>, <c>value</c>, <c>currency</c>,
    /// <c>start_date</c>, <c>end_date</c>, <c>auto_renew</c>, <c>renewal_term_months</c>, <c>notice_period_days</c>,
    /// <c>owner_id</c>, <c>metadata</c>, <c>external_references</c>, <c>parent_document_id</c>,
    /// <c>relation_type</c>, and <c>updated_at</c>. Dates are YYYY-MM-DD; monetary values use decimal.</param>
    /// <param name="accountId">Workspace; falls back to the client's default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The saved <see cref="DocumentContract"/> or <see cref="DocumentContractAmendment"/>.</returns>
    public Task<ContractRecord> SetAsync(string documentId, IDictionary<string, object?> terms,
        string? accountId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(terms);
        if (terms.Count == 0) throw new ValidationException("At least one contract attribute is required.");
        return CallAsync<ContractRecord>(ContractPath(documentId, accountId), HttpMethod.Put, terms,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /accounts/{accountId}/documents/{documentId}/contract/related</c> — read the root contract and all its instruments from any family document.</summary>
    /// <param name="documentId">Root contract document or linked amendment, renewal, or termination.</param>
    /// <param name="accountId">Workspace; falls back to the client's default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The root contract with its applied and pending amendments.</returns>
    public Task<DocumentContract> GetRelatedAsync(string documentId, string? accountId = null, CancellationToken cancellationToken = default) =>
        CallAsync<DocumentContract>($"{ContractPath(documentId, accountId)}/related", HttpMethod.Get,
            cancellationToken: cancellationToken);

    /// <summary>
    /// <c>POST /accounts/{accountId}/documents/{documentId}/contract/renew</c> — start the next term,
    /// reopening an expired contract. Terminated contracts are refused. Supply the current <c>term</c>
    /// or a later <c>end_date</c> so a repeated call cannot renew twice. The SDK never retries.
    /// </summary>
    /// <param name="documentId">Contract document to renew.</param>
    /// <param name="request">API attributes: <c>term</c> (current positive integer) or <c>end_date</c>
    /// (YYYY-MM-DD after the current end date); optional <c>value</c> (decimal; null clears it),
    /// and <c>updated_at</c> (read timestamp, giving 409 if stale). Without a date, the API advances
    /// by renewal_term_months (12 when unset) until the end date is in the future.</param>
    /// <param name="accountId">Workspace; falls back to the client's default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The renewed contract with its incremented term.</returns>
    public Task<DocumentContract> RenewAsync(string documentId, IDictionary<string, object?> request,
        string? accountId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if ((!request.TryGetValue("term", out var term) || term is null) &&
            (!request.TryGetValue("end_date", out var endDate) || endDate is not string date || string.IsNullOrWhiteSpace(date)))
            throw new ValidationException("A renewal requires the current term or a new end_date.");
        return CallAsync<DocumentContract>($"{ContractPath(documentId, accountId)}/renew", HttpMethod.Post, request,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>POST /accounts/{accountId}/documents/{documentId}/contract/terminate</c> — close a contract and stop its lifecycle alerts.</summary>
    /// <param name="documentId">Contract document to terminate.</param>
    /// <param name="request">Optional API attributes: <c>reason</c> (up to 500 characters; HTML stripped)
    /// and <c>updated_at</c> (read timestamp, giving 409 if stale).</param>
    /// <param name="accountId">Workspace; falls back to the client's default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The terminated contract.</returns>
    public Task<DocumentContract> TerminateAsync(string documentId, IDictionary<string, object?>? request = null,
        string? accountId = null, CancellationToken cancellationToken = default) =>
        CallAsync<DocumentContract>($"{ContractPath(documentId, accountId)}/terminate", HttpMethod.Post, request,
            cancellationToken: cancellationToken);

    private string ContractPath(string documentId, string? accountId) =>
        $"accounts/{AccountId(accountId)}/documents/{PathSegment(documentId, "Document ID")}/contract";
}
