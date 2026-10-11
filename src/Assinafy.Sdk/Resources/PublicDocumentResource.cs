using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Public document lookup and signer token delivery endpoints.</summary>
public sealed class PublicDocumentResource : BaseResource
{
    internal PublicDocumentResource(HttpClient http, Action<HttpRequestMessage>? authenticate = null)
        : base(http, authenticate: authenticate) { }

    /// <summary>Legacy <c>GET /public/documents/{document_id}</c> projection retained for compatibility. Use <see cref="GetDetailsAsync"/> for the complete documented <see cref="DocumentDetails"/> payload.</summary>
    /// <param name="documentId">Document whose public metadata to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The legacy public-document projection.</returns>
    [Obsolete("Use GetDetailsAsync, which returns the complete Document payload documented by the current API.")]
    public Task<PublicDocumentInfo> GetAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var document = PathSegment(documentId, "Document ID");
        return CallAsync<PublicDocumentInfo>(
            $"public/documents/{document}",
            HttpMethod.Get,
            cancellationToken: cancellationToken,
            authenticate: false);
    }

    /// <summary><c>GET /public/documents/{document_id}</c> — fetch the complete public document payload (no authentication required).</summary>
    /// <param name="documentId">Document whose public details to fetch.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete document payload returned by the public endpoint.</returns>
    public Task<DocumentDetails> GetDetailsAsync(
        string documentId,
        CancellationToken cancellationToken = default)
    {
        var document = PathSegment(documentId, "Document ID");
        return CallAsync<DocumentDetails>(
            $"public/documents/{document}",
            HttpMethod.Get,
            cancellationToken: cancellationToken,
            authenticate: false);
    }

    /// <summary>
    /// <c>PUT /public/documents/{document_id}/send-token</c> — email a one-time access token to a
    /// signer of a public document. Sends <c>{ "channel": "email", "recipient": email }</c> without
    /// credentials; the response data is discarded. Use
    /// <see cref="SendTokenAsync(string, SendDocumentTokenRequest, CancellationToken)"/> to receive it.
    /// </summary>
    /// <param name="documentId">Document whose access token to send; it must be pending signature.</param>
    /// <param name="email">Required. Email address of a signer of the document who has not signed yet.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the token has been sent.</returns>
    /// <exception cref="ArgumentException"><paramref name="email"/> is null or blank.</exception>
    public Task SendTokenAsync(
        string documentId,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return SendTokenAsync(
            documentId,
            new SendDocumentTokenRequest { Channel = SendDocumentTokenChannels.Email, Recipient = email },
            cancellationToken);
    }

    /// <summary>
    /// <c>PUT /public/documents/{document_id}/send-token</c> — send a one-time access token to a
    /// signer of a public document, so they can view it. Public endpoint: no credentials are sent.
    /// </summary>
    /// <remarks>
    /// Request body: <c>{ "channel": "email", "recipient": "signer@example.com" }</c>; both are
    /// required and <c>email</c> is the only channel (sent lowercase whatever its casing in
    /// <paramref name="request"/>). The document must be pending signature and the recipient must
    /// belong to a signer who has not signed yet.
    /// Response <c>data</c>: <c>{ "document": { "resource", "id", "name", "page_count", "created_by" }, "channel", "recipient" }</c>.
    /// Errors: <c>400</c> validation, <c>404</c> unknown document.
    /// </remarks>
    /// <param name="documentId">Document whose access token to send.</param>
    /// <param name="request">Channel (<see cref="SendDocumentTokenChannels.Email"/>) and the signer's email address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document summary, channel, and recipient the token was sent to.</returns>
    /// <exception cref="ValidationException">The channel is not <c>email</c>.</exception>
    public Task<SendDocumentTokenResult> SendTokenAsync(
        string documentId,
        SendDocumentTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var document = PathSegment(documentId, "Document ID");
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Channel);
        if (!string.Equals(request.Channel, SendDocumentTokenChannels.Email, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Only the email channel is supported for public document tokens.");

        return CallAsync<SendDocumentTokenResult>(
            $"public/documents/{document}/send-token",
            HttpMethod.Put,
            new Dictionary<string, string>
            {
                ["channel"] = SendDocumentTokenChannels.Email,
                ["recipient"] = request.Recipient,
            },
            cancellationToken,
            authenticate: false);
    }
}
