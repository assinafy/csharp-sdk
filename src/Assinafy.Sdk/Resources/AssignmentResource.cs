using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Assignments resource: create requests, estimate costs, resend, expiration, and account-facing identity reviews.</summary>
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
    /// <exception cref="ValidationException">A <c>DigitalCertificate</c> signer is combined with a method other than <c>virtual</c>; <see cref="ValidationException.Code"/> is <c>digital_certificate_requires_virtual</c>.</exception>
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
    /// <exception cref="ValidationException">A <c>DigitalCertificate</c> signer is combined with a method other than <c>virtual</c>; <see cref="ValidationException.Code"/> is <c>digital_certificate_requires_virtual</c>.</exception>
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
    /// <remarks>
    /// Request body: <c>{ "expires_at": "2026-12-31T23:59:59Z" }</c> or <c>{ "expires_at": null }</c>.
    /// The key is always sent: a date must be at least one hour in the future, and <see langword="null"/>
    /// removes the expiration. Not allowed once the document is closed (<c>400</c>).
    /// Response <c>data</c>: the updated <see cref="Assignment"/>.
    /// </remarks>
    /// <param name="documentId">Document that owns the assignment.</param>
    /// <param name="assignmentId">Assignment whose expiration to change.</param>
    /// <param name="expiresAt">New expiration as an ISO-8601 date-time string, or <see langword="null"/> to clear the expiration. A blank string is rejected.</param>
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
        if (expiresAt is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(expiresAt);

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

    /// <summary><c>GET /documents/{documentId}/assignments/{assignmentId}/signers/{signerId}/identity-verification</c> — get the account-facing identity review, evidence summary, checks, and available image names.</summary>
    /// <param name="documentId">Document that owns the assignment.</param>
    /// <param name="assignmentId">Assignment containing the signer.</param>
    /// <param name="signerId">Signer whose verification to review.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The review summary; directly approved verifications have no evidence here. Returns 404 when no verification exists.</returns>
    public Task<SignerIdentityReview> GetIdentityReviewAsync(
        string documentId,
        string assignmentId,
        string signerId,
        CancellationToken cancellationToken = default) =>
        CallAsync<SignerIdentityReview>(
            IdentityReviewPath(documentId, assignmentId, signerId),
            HttpMethod.Get,
            cancellationToken: cancellationToken);

    /// <summary><c>GET /documents/{documentId}/assignments/{assignmentId}/signers/{signerId}/identity-verification/images/{name}</c> — fetch evidence directly from the provider while the verification is InReview. Returns 404 after a decision.</summary>
    /// <param name="documentId">Document that owns the assignment.</param>
    /// <param name="assignmentId">Assignment containing the signer.</param>
    /// <param name="signerId">Signer whose evidence to fetch.</param>
    /// <param name="name">An entry from the review's Images: document_front, document_back, document_pdf, or face.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>JPEG, PNG, or PDF bytes. The API sends Cache-Control: no-store; handle these sensitive bytes without caching or logging.</returns>
    public Task<byte[]> DownloadIdentityReviewImageAsync(
        string documentId,
        string assignmentId,
        string signerId,
        string name,
        CancellationToken cancellationToken = default) =>
        CallBinaryAsync(
            $"{IdentityReviewPath(documentId, assignmentId, signerId)}/images/{PathSegment(name, "Image name")}",
            HttpMethod.Get,
            cancellationToken);

    /// <summary><c>POST /documents/{documentId}/assignments/{assignmentId}/signers/{signerId}/identity-verification/review</c> — approve or reject an InReview verification on a pending_signature or pending_identity_review document.</summary>
    /// <remarks>Approval permits certification once all signers have signed and all reviews are approved. Rejection cancels the document as rejected_by_user and notifies signers and copy receivers. The signer receives no separate decision message.</remarks>
    /// <param name="documentId">Document that owns the assignment.</param>
    /// <param name="assignmentId">Assignment containing the signer.</param>
    /// <param name="signerId">Signer whose identity to review.</param>
    /// <param name="request">Approved or Rejected decision and an optional private note of up to 2000 characters.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated identity review, including reviewer, note, and decision timestamps.</returns>
    public Task<SignerIdentityReview> DecideIdentityReviewAsync(
        string documentId,
        string assignmentId,
        string signerId,
        DecideIdentityReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Decision is not ("Approved" or "Rejected"))
            throw new ValidationException("Identity review decision must be Approved or Rejected.");
        if (request.Note is not null && request.Note.EnumerateRunes().Count() > 2000)
            throw new ValidationException("Identity review note must not exceed 2000 characters.");

        return CallAsync<SignerIdentityReview>(
            $"{IdentityReviewPath(documentId, assignmentId, signerId)}/review",
            HttpMethod.Post,
            request,
            cancellationToken: cancellationToken);
    }

    private static string IdentityReviewPath(string documentId, string assignmentId, string signerId) =>
        $"documents/{PathSegment(documentId, "Document ID")}/assignments/{PathSegment(assignmentId, "Assignment ID")}/signers/{PathSegment(signerId, "Signer ID")}/identity-verification";

    internal static Dictionary<string, object?> BuildPayload(CreateAssignmentRequest request)
    {
        ValidateEntries(request.Entries);
        var signerRefs = ExtractSignerRefs(request);
        if (signerRefs.Count == 0)
            throw new ValidationException("At least one signer is required.");
        ValidateDigitalCertificateMethod(request.Method, signerRefs.Select(signer => signer.VerificationMethod));

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
        ValidateDigitalCertificateMethod(request.Method, signerRefs.Select(signer => signer.VerificationMethod));

        body["signers"] = signerRefs.Select(reference =>
        {
            var signer = new Dictionary<string, object?>();
            if (!string.IsNullOrWhiteSpace(reference.VerificationMethod))
                signer["verification_method"] = reference.VerificationMethod;
            if (reference.NotificationMethods is not null)
                signer["notification_methods"] = reference.NotificationMethods;
            return signer;
        }).ToList();

        if (request.Entries?.Count > 0) body["entries"] = request.Entries;
        return body;
    }

    internal static void ValidateDigitalCertificateMethod(
        string? method,
        IEnumerable<string?> verificationMethods)
    {
        if (!string.Equals(method ?? AssignmentMethods.Virtual, AssignmentMethods.Virtual, StringComparison.Ordinal) &&
            verificationMethods.Contains(SignerChannels.DigitalCertificate))
            throw new ValidationException(
                "DigitalCertificate signers require a virtual assignment.",
                new Dictionary<string, object?> { ["code"] = "digital_certificate_requires_virtual" });
    }

    private static void ValidateEntries(IReadOnlyList<AssignmentEntry>? entries)
    {
        if (entries is null) return;

        foreach (var entry in entries)
        {
            if (entry is null || string.IsNullOrWhiteSpace(entry.PageId))
                throw new ValidationException("Assignment entry page ID is required.");

            if (entry.Fields is null)
                throw new ValidationException("Assignment entry fields are required.");

            foreach (var field in entry.Fields)
            {
                if (field is null || string.IsNullOrWhiteSpace(field.SignerId) || string.IsNullOrWhiteSpace(field.FieldId))
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
        {
            if (request.Signers.Any(signer => signer is null))
                throw new ValidationException("Signer references must not contain null entries.");
            return request.Signers;
        }

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

        if (reference.NotificationMethods is not null)
            result["notification_methods"] = reference.NotificationMethods;

        if (reference.Step.HasValue)
            result["step"] = reference.Step.Value;

        return result;
    }
}
