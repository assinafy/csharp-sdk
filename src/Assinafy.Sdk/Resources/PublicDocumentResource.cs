using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Public document lookup and signer token delivery endpoints.</summary>
public sealed class PublicDocumentResource : BaseResource
{
    internal PublicDocumentResource(HttpClient http) : base(http) { }

    /// <summary><c>GET /public/documents/{document_id}</c> — fetch a document's public metadata (no authentication required).</summary>
    public Task<PublicDocumentInfo> GetAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var document = RequireId(documentId, "Document ID");
        return CallAsync<PublicDocumentInfo>(
            $"public/documents/{document}",
            HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>PUT /public/documents/{document_id}/send-token</c> — deliver a signing access token to a recipient over email or WhatsApp.</summary>
    public Task<SendDocumentTokenResult> SendTokenAsync(
        string documentId,
        SendDocumentTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var document = RequireId(documentId, "Document ID");
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Channel);

        return CallAsync<SendDocumentTokenResult>(
            $"public/documents/{document}/send-token",
            HttpMethod.Put,
            request,
            cancellationToken: cancellationToken);
    }
}
