using System.Net;

namespace Assinafy.Sdk.Support;

/// <summary>HTTP stream content that leaves the caller-owned stream open.</summary>
internal sealed class NonDisposingStreamContent(Stream stream) : HttpContent
{
    protected override Task SerializeToStreamAsync(Stream target, TransportContext? context) =>
        stream.CopyToAsync(target);

    protected override Task SerializeToStreamAsync(
        Stream target,
        TransportContext? context,
        CancellationToken cancellationToken) => stream.CopyToAsync(target, cancellationToken);

    protected override bool TryComputeLength(out long length)
    {
        if (stream.CanSeek)
        {
            length = stream.Length - stream.Position;
            return true;
        }

        length = 0;
        return false;
    }
}
