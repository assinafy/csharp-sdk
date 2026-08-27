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

    /// <summary><c>PUT /public/documents/{document_id}/send-token</c> — ask the API to email a signing access token.</summary>
    /// <param name="documentId">Document whose signing token to send.</param>
    /// <param name="email">Optional destination email. When omitted, the API uses the document's configured recipient.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the token-delivery request has been accepted.</returns>
    public Task SendTokenAsync(
        string documentId,
        string? email = null,
        CancellationToken cancellationToken = default)
    {
        var document = PathSegment(documentId, "Document ID");
        if (email is not null)
            ArgumentException.ThrowIfNullOrWhiteSpace(email);

        object? body = email is null ? null : new { email };
        return CallVoidAsync(
            $"public/documents/{document}/send-token",
            HttpMethod.Put,
            body,
            cancellationToken,
            authenticate: false);
    }

    /// <summary>Legacy send-token overload retained for compatibility. The current API accepts only an optional email and returns no data.</summary>
    /// <param name="documentId">Document whose signing token to send.</param>
    /// <param name="request">Recipient address and delivery channel (<see cref="SignerChannels"/>: <c>Email</c> or <c>Whatsapp</c>; WhatsApp is paid-only).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A compatibility result containing the requested channel and recipient.</returns>
    [Obsolete("Use SendTokenAsync(documentId, email, cancellationToken). The current API returns no data and does not accept a channel.")]
    public async Task<SendDocumentTokenResult> SendTokenAsync(
        string documentId,
        SendDocumentTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var document = RequireId(documentId, "Document ID");
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Recipient);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Channel);

        if (string.Equals(request.Channel, SignerChannels.Email, StringComparison.OrdinalIgnoreCase))
        {
            await SendTokenAsync(document, request.Recipient, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            document = PathSegment(document, "Document ID");
            await CallVoidAsync(
                $"public/documents/{document}/send-token",
                HttpMethod.Put,
                request,
                cancellationToken,
                authenticate: false).ConfigureAwait(false);
        }

        return new SendDocumentTokenResult
        {
            Channel = request.Channel,
            Recipient = request.Recipient,
        };
    }
}
