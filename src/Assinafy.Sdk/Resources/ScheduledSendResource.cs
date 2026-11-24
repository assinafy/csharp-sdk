using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Scheduled assignments and envelopes. Available on staging; OAuth requires documents:write, including listing.</summary>
public sealed class ScheduledSendResource : BaseResource
{
    internal ScheduledSendResource(HttpClient http, string? defaultAccountId = null, Action<HttpRequestMessage>? authenticate = null)
        : base(http, defaultAccountId, authenticate) { }

    /// <summary><c>POST /documents/{documentId}/scheduled-sends</c> — retain an assignment request for later. Nothing is sent or charged now; checks and charges run as the scheduling member when due. No retries.</summary>
    /// <param name="documentId">Unassigned document without another pending/processing scheduled send.</param>
    /// <param name="request">The normal assignment request, including any credit ceiling.</param>
    /// <param name="sendAt">One hour to ninety days ahead. Any expiration must be at least two hours later. The API checks these bounds.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pending scheduled send. The server checks due sends every five minutes.</returns>
    public Task<ScheduledSend> ScheduleAssignmentAsync(string documentId, CreateAssignmentRequest request,
        DateTimeOffset sendAt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var body = AssignmentResource.BuildPayload(request);
        body["send_at"] = sendAt.ToUnixTimeSeconds();
        return CallAsync<ScheduledSend>($"documents/{PathSegment(documentId, "Document ID")}/scheduled-sends",
            HttpMethod.Post, body, cancellationToken: cancellationToken);
    }

    /// <summary><c>POST /accounts/{accountId}/scheduled-sends</c> — retain an envelope request for later. Requires Envelopes; nothing is created or charged now. Each call creates a send; idempotency keys are not supported.</summary>
    /// <param name="request">The normal envelope request, including any credit ceiling. Documents must have no pending/processing scheduled send.</param>
    /// <param name="sendAt">One hour to ninety days ahead; each expiration must be at least two hours later. Checked by the API.</param>
    /// <param name="accountId">Workspace; falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The pending scheduled send. Balances and membership are checked again when sent.</returns>
    public Task<ScheduledSend> ScheduleEnvelopeAsync(CreateEnvelopeRequest request, DateTimeOffset sendAt,
        string? accountId = null, CancellationToken cancellationToken = default)
    {
        EnvelopeResource.ValidateRequest(request);
        if (request.MaxTotalCredits < 0) throw new ValidationException("Credit ceiling must not be negative.");
        return CallAsync<ScheduledSend>($"accounts/{AccountId(accountId)}/scheduled-sends", HttpMethod.Post,
            new
            {
                request.Name,
                request.Message,
                request.Signers,
                request.Documents,
                request.MaxTotalCredits,
                SendAt = sendAt.ToUnixTimeSeconds()
            }, cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /accounts/{accountId}/scheduled-sends</c> — list all scheduled-send states, newest first. Requires documents:write because failures can reveal balance and membership information.</summary>
    /// <param name="queryParams">Optional document_id, page and per-page; twenty per page by default.</param>
    /// <param name="accountId">Workspace; falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The requested page with pagination metadata.</returns>
    public Task<PaginatedResult<ScheduledSend>> ListAsync(IDictionary<string, string?>? queryParams = null,
        string? accountId = null, CancellationToken cancellationToken = default) =>
        CallListAsync<ScheduledSend>($"accounts/{AccountId(accountId)}/scheduled-sends", queryParams, cancellationToken);

    /// <summary><c>PUT /accounts/{accountId}/scheduled-sends/{scheduledSendId}/cancel</c> — cancel a pending send without charging. Other states return 409.</summary>
    /// <param name="scheduledSendId">Scheduled send identifier.</param>
    /// <param name="accountId">Workspace; falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The cancelled scheduled send.</returns>
    public Task<ScheduledSend> CancelAsync(string scheduledSendId, string? accountId = null, CancellationToken cancellationToken = default) =>
        CallAsync<ScheduledSend>($"{ScheduledSendPath(scheduledSendId, accountId)}/cancel", HttpMethod.Put,
            cancellationToken: cancellationToken);

    /// <summary><c>PUT /accounts/{accountId}/scheduled-sends/{scheduledSendId}/send</c> — send a pending request now as its scheduling member. Normal create checks and charges apply; refusal leaves it pending. No retries.</summary>
    /// <param name="scheduledSendId">Scheduled send identifier. Non-pending states return 409.</param>
    /// <param name="accountId">Workspace; falls back to the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sent request with the created assignment or envelope ID.</returns>
    public Task<ScheduledSend> SendNowAsync(string scheduledSendId, string? accountId = null, CancellationToken cancellationToken = default) =>
        CallAsync<ScheduledSend>($"{ScheduledSendPath(scheduledSendId, accountId)}/send", HttpMethod.Put,
            cancellationToken: cancellationToken);

    private string ScheduledSendPath(string scheduledSendId, string? accountId) =>
        $"accounts/{AccountId(accountId)}/scheduled-sends/{PathSegment(scheduledSendId, "Scheduled send ID")}";
}
