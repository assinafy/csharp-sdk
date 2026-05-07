using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Assignments resource: create requests, estimate costs, resend, and expiration handling.</summary>
public sealed class AssignmentResource : BaseResource
{
    internal AssignmentResource(HttpClient http)
        : base(http) { }

    public Task<Assignment> CreateAsync(
        string documentId,
        CreateAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var document = RequireId(documentId, "Document ID");

        return CallAsync<Assignment>(
            $"documents/{document}/assignments",
            HttpMethod.Post,
            BuildPayload(request),
            cancellationToken: cancellationToken);
    }

    public Task<AssignmentCostEstimate> EstimateCostAsync(
        string documentId,
        CreateAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var document = RequireId(documentId, "Document ID");

        return CallAsync<AssignmentCostEstimate>(
            $"documents/{document}/assignments/estimate-cost",
            HttpMethod.Post,
            BuildPayload(request, allowSignersWithoutId: true),
            cancellationToken: cancellationToken);
    }

    public Task<Assignment> ResetExpirationAsync(
        string documentId,
        string assignmentId,
        string? expiresAt,
        CancellationToken cancellationToken = default)
    {
        var document = RequireId(documentId, "Document ID");
        var assignment = RequireId(assignmentId, "Assignment ID");

        return CallAsync<Assignment>(
            $"documents/{document}/assignments/{assignment}/reset-expiration",
            HttpMethod.Put,
            new Dictionary<string, object?> { ["expires_at"] = expiresAt },
            cancellationToken: cancellationToken);
    }

    public Task<ResendNotificationResult> ResendNotificationAsync(
        string documentId,
        string assignmentId,
        string signerId,
        CancellationToken cancellationToken = default)
    {
        var document = RequireId(documentId, "Document ID");
        var assignment = RequireId(assignmentId, "Assignment ID");
        var signer = RequireId(signerId, "Signer ID");

        return CallAsync<ResendNotificationResult>(
            $"documents/{document}/assignments/{assignment}/signers/{signer}/resend",
            HttpMethod.Put,
            cancellationToken: cancellationToken);
    }

    public Task<ResendCostEstimate> EstimateResendCostAsync(
        string documentId,
        string assignmentId,
        string signerId,
        CancellationToken cancellationToken = default)
    {
        var document = RequireId(documentId, "Document ID");
        var assignment = RequireId(assignmentId, "Assignment ID");
        var signer = RequireId(signerId, "Signer ID");

        return CallAsync<ResendCostEstimate>(
            $"documents/{document}/assignments/{assignment}/signers/{signer}/estimate-resend-cost",
            HttpMethod.Post,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<WhatsAppNotification>> ListWhatsAppNotificationsAsync(
        string documentId,
        string assignmentId,
        CancellationToken cancellationToken = default)
    {
        var document = RequireId(documentId, "Document ID");
        var assignment = RequireId(assignmentId, "Assignment ID");

        var result = await CallAsync<List<WhatsAppNotification>>(
            $"documents/{document}/assignments/{assignment}/whatsapp-notifications",
            HttpMethod.Get,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result ?? [];
    }

    internal static Dictionary<string, object?> BuildPayload(
        CreateAssignmentRequest request,
        bool allowSignersWithoutId = false)
    {
        var signerRefs = ExtractSignerRefs(request);
        if (signerRefs.Count == 0)
            throw new ValidationException("At least one signer is required.");

        var signers = signerRefs
            .Select(r => NormaliseSignerRef(r, allowSignersWithoutId))
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
        if (request.SignerIds?.Length > 0) body["signer_ids"] = request.SignerIds;

        return body;
    }

    private static List<SignerRef> ExtractSignerRefs(CreateAssignmentRequest request)
    {
        if (request.Signers?.Count > 0)
            return request.Signers;

        if (request.SignerIds?.Length > 0)
            return request.SignerIds.Select(id => (SignerRef)id).ToList();

        return [];
    }

    private static Dictionary<string, object?> NormaliseSignerRef(SignerRef reference, bool allowWithoutId)
    {
        var result = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(reference.Id))
            result["id"] = reference.Id;

        if (!string.IsNullOrWhiteSpace(reference.VerificationMethod))
            result["verification_method"] = reference.VerificationMethod;

        if (reference.NotificationMethods?.Length > 0)
            result["notification_methods"] = reference.NotificationMethods;

        if (string.IsNullOrWhiteSpace(reference.Id) && !allowWithoutId)
            throw new ValidationException("Invalid signer reference: ID is required for this operation.");

        return result;
    }
}
