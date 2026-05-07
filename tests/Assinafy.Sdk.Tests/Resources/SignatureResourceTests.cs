using System.Text;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class SignatureResourceTests
{
    private static SignatureResource CreateResource(FakeHttpMessageHandler handler)
        => new(FakeHttpMessageHandler.CreateClient(handler));

    [Fact]
    public async Task Upload_PostsImageToSignatureEndpoint()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/signature?signer-access-code=access&type=signature",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));

        var resource = CreateResource(handler);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("png"));
        await resource.UploadAsync(stream, "access");

        handler.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Post &&
            r.RequestUri!.PathAndQuery.Contains("/signature?signer-access-code=access&type=signature"));
    }

    [Fact]
    public async Task Download_GetsSignatureImage()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/signature/signature?signer-access-code=access", "bytes");

        var resource = CreateResource(handler);
        var result = await resource.DownloadAsync("access");

        result.Should().NotBeEmpty();
    }
}
