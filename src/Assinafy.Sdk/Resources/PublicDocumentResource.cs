using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Public document lookup and signer token delivery endpoints.</summary>
public sealed class PublicDocumentResource : BaseResource
{
    internal PublicDocumentResource(HttpClient http) : base(http) { }

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
