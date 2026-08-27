using System.Net;
using Assinafy.Sdk.Exceptions;

namespace Assinafy.Sdk.Support;

/// <summary>HTTP stream content that leaves the caller-owned stream open.</summary>
internal sealed class NonDisposingStreamContent : HttpContent
{
    private readonly Stream _stream;

    public NonDisposingStreamContent(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ValidationException("Upload stream must be readable.");
        if (stream.CanSeek && stream.Position > stream.Length)
            throw new ValidationException("Upload stream position must not exceed its length.");

        _stream = stream;
    }

    protected override Task SerializeToStreamAsync(Stream target, TransportContext? context) =>
        _stream.CopyToAsync(target);

    protected override Task SerializeToStreamAsync(
        Stream target,
        TransportContext? context,
        CancellationToken cancellationToken) => _stream.CopyToAsync(target, cancellationToken);

    protected override bool TryComputeLength(out long length)
    {
        if (_stream.CanSeek)
        {
            length = _stream.Length - _stream.Position;
            if (length < 0)
            {
                length = 0;
                return false;
            }

            return true;
        }

        length = 0;
        return false;
    }
}
