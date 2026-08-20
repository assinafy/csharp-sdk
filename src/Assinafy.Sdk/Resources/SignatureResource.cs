using System.Net.Http.Headers;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Support;

namespace Assinafy.Sdk.Resources;

/// <summary>Signer signature and initial image upload/download endpoints.</summary>
public sealed class SignatureResource : BaseResource
{
    internal SignatureResource(HttpClient http, Action<HttpRequestMessage>? authenticate = null)
        : base(http, authenticate: authenticate) { }

    /// <summary><c>POST /signature?signer-access-code={code}&amp;type={type}</c> — upload the signer's signature or initial image.</summary>
    /// <param name="imageStream">PNG image data to upload. The SDK leaves this caller-owned stream open.</param>
    /// <param name="signerAccessCode">The signer's access code authorizing the upload.</param>
    /// <param name="type">Which image to upload; one of the <see cref="SignatureImageTypes"/> values (<c>signature</c> or <c>initial</c>). Defaults to <c>signature</c>.</param>
    /// <param name="contentType">MIME type of the image data. The current API documents <c>image/png</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task UploadAsync(
        Stream imageStream,
        string signerAccessCode,
        string type = SignatureImageTypes.Signature,
        string contentType = "image/png",
        CancellationToken cancellationToken = default)
        => UploadCoreAsync(
            imageStream,
            signerAccessCode,
            reuse: null,
            type,
            contentType,
            cancellationToken);

    /// <summary><c>POST /signature?signer-access-code={code}&amp;type={type}&amp;reuse={reuse}</c> — upload a signature or initial and choose whether it may be reused.</summary>
    /// <param name="imageStream">PNG image data to upload. The SDK leaves this caller-owned stream open.</param>
    /// <param name="signerAccessCode">The signer's access code authorizing the upload.</param>
    /// <param name="reuse">Whether the uploaded image may be reused in future signing processes.</param>
    /// <param name="type">Which image to upload; <c>signature</c> or <c>initial</c>.</param>
    /// <param name="contentType">MIME type of the image data. The current API documents <c>image/png</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task UploadAsync(
        Stream imageStream,
        string signerAccessCode,
        bool reuse,
        string type = SignatureImageTypes.Signature,
        string contentType = "image/png",
        CancellationToken cancellationToken = default)
        => UploadCoreAsync(
            imageStream,
            signerAccessCode,
            reuse,
            type,
            contentType,
            cancellationToken);

    private Task UploadCoreAsync(
        Stream imageStream,
        string signerAccessCode,
        bool? reuse,
        string type,
        string contentType,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        var code = RequireId(signerAccessCode, "Signer access code");
        var imageType = RequireId(type, "Signature image type");
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var query = AccessCodeQuery(code);
        query["type"] = imageType;
        if (reuse.HasValue)
            query["reuse"] = reuse.Value ? "true" : "false";
        var path = AppendQueryString("signature", query);

        var content = new NonDisposingStreamContent(imageStream);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        return CallContentVoidAsync(
            path,
            HttpMethod.Post,
            content,
            cancellationToken: cancellationToken,
            authenticate: false);
    }

    /// <summary><c>GET /signature/{type}?signer-access-code={code}</c> — download the signer's signature or initial image.</summary>
    /// <param name="signerAccessCode">The signer's access code authorizing the download.</param>
    /// <param name="type">Which image to download; one of the <see cref="SignatureImageTypes"/> values (<c>signature</c> or <c>initial</c>). Defaults to <c>signature</c>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<byte[]> DownloadAsync(
        string signerAccessCode,
        string type = SignatureImageTypes.Signature,
        CancellationToken cancellationToken = default)
    {
        var code = RequireId(signerAccessCode, "Signer access code");
        var imageType = RequireId(type, "Signature image type");

        var path = AppendQueryString($"signature/{imageType}", AccessCodeQuery(code));

        return CallBinaryAsync(path, HttpMethod.Get, cancellationToken, authenticate: false);
    }
}
