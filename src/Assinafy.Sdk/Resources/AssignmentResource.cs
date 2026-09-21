using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Assignments resource: create requests, estimate costs, resend, and expiration handling.</summary>
public sealed class AssignmentResource : BaseResource
{
    internal AssignmentResource(HttpClient http, string? defaultAccountId = null, Action<HttpRequestMessage>? authenticate = null)
        : base(http, defaultAccountId, authenticate) { }

    /// <summary>
    /// <c>GET /assignments</c> — list assignments for the authenticated user's current account.
    /// Passing <paramref name="accountId"/> explicitly sends the legacy, undocumented
    /// <c>accountId</c> compatibility extension; the configured client default is not sent.
    /// </summary>
    /// <param name="parameters">Optional pagination (<c>page</c>, <c>per-page</c>).</param>
    /// <param name="accountId">Optional legacy account query extension. Leave <see langword="null"/> for the documented current-account request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The requested page of assignments and any pagination metadata returned by the API.</returns>
    public Task<PaginatedResult<Assignment>> ListAsync(
        AssignmentListParams? parameters = null,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var query = new Dictionary<string, string?>();
        if (accountId is not null) query["accountId"] = RequireId(accountId, "Account ID");
        if (parameters?.Page is int page) query["page"] = page.ToString();
        if (parameters?.PerPage is int perPage) query["per-page"] = perPage.ToString();

        return CallListAsync<Assignment>("assignments", query, cancellationToken);
    }

    /// <summary><c>POST /documents/{document_id}/assignments</c> — create a signature assignment binding signers to a document.</summary>
    /// <param name="documentId">Document to attach the assignment to.</param>
    /// <param name="request">Assignment configuration: <c>method</c> (<c>virtual</c> or <c>collect</c>, see <see cref="AssignmentMethods"/>; defaults to <c>virtual</c>), the signers, and optional message, <c>expires_at</c>, copy receivers, and entries.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The newly created assignment.</returns>
    public Task<Assignment> CreateAsync(
        string documentId,
        CreateAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var document = PathSegment(documentId, "Document ID");

        return CallAsync<Assignment>(
            $"documents/{document}/assignments",
            HttpMethod.Post,
            BuildPayload(request),
            cancellationToken: cancellationToken);
    }

    /// <summary><c>POST /documents/{documentId}/assignments/estimate-cost</c> — preview the credit cost of <see cref="CreateAsync"/> before committing.</summary>
    /// <param name="documentId">Document the assignment would target.</param>
    /// <param name="request">Same shape as <see cref="CreateAsync"/>; for estimation, signers may be specified without an <c>id</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The estimated document and credit cost, balance information, and any blocking reason.</returns>
    public Task<AssignmentCostEstimate> EstimateCostAsync(
        string documentId,
        CreateAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var document = PathSegment(documentId, "Document ID");

        return CallAsync<AssignmentCostEstimate>(
            $"documents/{document}/assignments/estimate-cost",
            HttpMethod.Post,
            BuildEstimatePayload(request),
            cancellationToken: cancellationToken);
    }

    /// <summary><c>PUT /documents/{documentId}/assignments/{assignmentId}/reset-expiration</c> — set or clear the assignment's expiration date.</summary>
    /// <param name="documentId">Document that owns the assignment.</param>
    /// <param name="assignmentId">Assignment whose expiration to change.</param>
    /// <param name="expiresAt">New expiration as an ISO-8601 date-time string, or <see langword="null"/> to clear the expiration.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The assignment with its updated expiration.</returns>
    public Task<Assignment> ResetExpirationAsync(
        string documentId,
        string assignmentId,
        string? expiresAt,
        CancellationToken cancellationToken = default)
    {
        var document = PathSegment(documentId, "Document ID");
        var assignment = PathSegment(assignmentId, "Assignment ID");

        return CallAsync<Assignment>(
            $"documents/{document}/assignments/{assignment}/reset-expiration",
            HttpMethod.Put,
            new Dictionary<string, object?> { ["expires_at"] = expiresAt },
            cancellationToken: cancellationToken);
    }

    /// <summary><c>PUT /documents/{documentId}/assignments/{assignmentId}/signers/{signerId}/resend</c> — resend the signature notification (email or WhatsApp) for a specific signer.</summary>
    /// <param name="documentId">Document that owns the assignment.</param>
    /// <param name="assignmentId">Assignment the signer belongs to.</param>
    /// <param name="signerId">Signer to re-notify.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The notification result, including the document and signer IDs and whether it was sent.</returns>
    public Task<ResendNotificationResult> ResendNotificationAsync(
        string documentId,
        string assignmentId,
        string signerId,
        CancellationToken cancellationToken = default)
    {
        var document = PathSegment(documentId, "Document ID");
        var assignment = PathSegment(assignmentId, "Assignment ID");
        var signer = PathSegment(signerId, "Signer ID");

        return CallAsync<ResendNotificationResult>(
            $"documents/{document}/assignments/{assignment}/signers/{signer}/resend",
            HttpMethod.Put,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>POST /documents/{documentId}/assignments/{assignmentId}/signers/{signerId}/estimate-resend-cost</c> — preview the credit cost of <see cref="ResendNotificationAsync"/>.</summary>
    /// <param name="documentId">Document that owns the assignment.</param>
    /// <param name="assignmentId">Assignment the signer belongs to.</param>
    /// <param name="signerId">Signer whose resend cost to estimate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The estimated resend cost, balance information, and any blocking reason.</returns>
    public Task<ResendCostEstimate> EstimateResendCostAsync(
        string documentId,
        string assignmentId,
        string signerId,
        CancellationToken cancellationToken = default)
    {
        var document = PathSegment(documentId, "Document ID");
        var assignment = PathSegment(assignmentId, "Assignment ID");
        var signer = PathSegment(signerId, "Signer ID");

        return CallAsync<ResendCostEstimate>(
            $"documents/{document}/assignments/{assignment}/signers/{signer}/estimate-resend-cost",
            HttpMethod.Post,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /documents/{documentId}/assignments/{assignmentId}/whatsapp-notifications</c> — list the rendered WhatsApp template messages dispatched for an assignment.</summary>
    /// <param name="documentId">Document that owns the assignment.</param>
    /// <param name="assignmentId">Assignment whose WhatsApp notifications to list.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rendered WhatsApp notifications sent for the assignment.</returns>
    public async Task<IReadOnlyList<WhatsAppNotification>> ListWhatsAppNotificationsAsync(
        string documentId,
        string assignmentId,
        CancellationToken cancellationToken = default)
    {
        var document = PathSegment(documentId, "Document ID");
        var assignment = PathSegment(assignmentId, "Assignment ID");

        return await CallListBodyAsync<WhatsAppNotification>(
            $"documents/{document}/assignments/{assignment}/whatsapp-notifications",
            HttpMethod.Get,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    internal static Dictionary<string, object?> BuildPayload(CreateAssignmentRequest request)
    {
        ValidateEntries(request.Entries);
        var signerRefs = ExtractSignerRefs(request);
        if (signerRefs.Count == 0)
            throw new ValidationException("At least one signer is required.");

        var signers = signerRefs
            .Select(NormaliseSignerRef)
            .ToList();

        var body = new Dictionary<string, object?>
        {
            ["method"] = request.Method ?? "virtual",
            ["signers"] = signers,
        };

        if (request.Message is not null) body["message"] = request.Message;
        if (request.ExpiresAt is not null) body["expires_at"] = request.ExpiresAt;
        if (request.CopyReceivers?.Length > 0) body["copy_receivers"] = request.CopyReceivers;
        if (request.Entries?.Count > 0) body["entries"] = request.Entries;

        return body;
    }

    internal static Dictionary<string, object?> BuildEstimatePayload(CreateAssignmentRequest request)
    {
        ValidateEntries(request.Entries);
        var body = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(request.Method)) body["method"] = request.Method;

        // The published contract marks `signers` required only for `virtual`, but the API prices
        // per signer in both methods and answers a signer-less estimate with
        // 400 "Pelo menos um signatários precisa ser informado."
        var signerRefs = ExtractSignerRefs(request);
        if (signerRefs.Count == 0)
            throw new ValidationException("At least one signer is required for a cost estimate.");

        body["signers"] = signerRefs.Select(reference =>
        {
            var signer = new Dictionary<string, object?>();
            if (!string.IsNullOrWhiteSpace(reference.VerificationMethod))
                signer["verification_method"] = reference.VerificationMethod;
            if (reference.NotificationMethods?.Length > 0)
                signer["notification_methods"] = reference.NotificationMethods;
            return signer;
        }).ToList();

        if (request.Entries?.Count > 0) body["entries"] = request.Entries;
        return body;
    }

    private static void ValidateEntries(IReadOnlyList<AssignmentEntry>? entries)
    {
        if (entries is null) return;

        foreach (var entry in entries)
        {
            if (string.IsNullOrWhiteSpace(entry.PageId))
                throw new ValidationException("Assignment entry page ID is required.");

            foreach (var field in entry.Fields)
            {
                if (string.IsNullOrWhiteSpace(field.SignerId) || string.IsNullOrWhiteSpace(field.FieldId))
                    throw new ValidationException("Assignment entry signer and field IDs are required.");

                var settings = field.DisplaySettings;
                if (settings is not null &&
                    (!double.IsFinite(settings.Left) || settings.Left < 0 ||
                     !double.IsFinite(settings.Top) || settings.Top < 0 ||
                     !double.IsFinite(settings.Width) || settings.Width <= 0 ||
                     !double.IsFinite(settings.Height) || settings.Height <= 0 ||
                     !double.IsFinite(settings.FontSize) || settings.FontSize <= 0))
                    throw new ValidationException("Display settings require non-negative left/top and positive width/height/font size.");
            }
        }
    }

    private static List<SignerRef> ExtractSignerRefs(CreateAssignmentRequest request)
    {
        if (request.Signers?.Count > 0)
            return request.Signers;

        if (request.SignerIds?.Length > 0)
            return request.SignerIds.Select(id => (SignerRef)id).ToList();

        return [];
    }

    private static Dictionary<string, object?> NormaliseSignerRef(SignerRef reference)
    {
        if (string.IsNullOrWhiteSpace(reference.Id))
            throw new ValidationException("Invalid signer reference: ID is required for this operation.");

        var result = new Dictionary<string, object?> { ["id"] = reference.Id };

        if (!string.IsNullOrWhiteSpace(reference.VerificationMethod))
            result["verification_method"] = reference.VerificationMethod;

        if (reference.NotificationMethods?.Length > 0)
            result["notification_methods"] = reference.NotificationMethods;

        if (reference.Step.HasValue)
            result["step"] = reference.Step.Value;

        return result;
    }
}
