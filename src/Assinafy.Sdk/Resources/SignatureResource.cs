using System.Net.Http.Headers;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Signer signature and initial image upload/download endpoints.</summary>
public sealed class SignatureResource : BaseResource
{
    internal SignatureResource(HttpClient http, Action<HttpRequestMessage>? authenticate = null)
        : base(http, authenticate: authenticate) { }

    /// <summary><c>POST /signature?signer-access-code={code}&amp;type={type}</c> — upload the signer's signature or initial image (image/png or image/jpeg).</summary>
    public Task UploadAsync(
        Stream imageStream,
        string signerAccessCode,
        string type = SignatureImageTypes.Signature,
        string contentType = "image/png",
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(imageStream);
        var code = RequireId(signerAccessCode, "Signer access code");
        var imageType = RequireId(type, "Signature image type");
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);

        var path = AppendQueryString("signature", new Dictionary<string, string?>
        {
            ["signer-access-code"] = code,
            ["type"] = imageType,
        });

        var content = new StreamContent(imageStream);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        return CallContentVoidAsync(path, HttpMethod.Post, content, cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /signature/{type}?signer-access-code={code}</c> — download the signer's signature or initial image.</summary>
    public Task<byte[]> DownloadAsync(
        string signerAccessCode,
        string type = SignatureImageTypes.Signature,
        CancellationToken cancellationToken = default)
    {
        var code = RequireId(signerAccessCode, "Signer access code");
        var imageType = RequireId(type, "Signature image type");

        var path = AppendQueryString($"signature/{imageType}", new Dictionary<string, string?>
        {
            ["signer-access-code"] = code,
        });

        return CallBinaryAsync(path, HttpMethod.Get, cancellationToken);
    }
}
