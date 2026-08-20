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
    public async Task Upload_PostsReuseFlagWithoutUserCredentialsAndLeavesStreamOpen()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/signature?signer-access-code=access&type=signature&reuse=true",
            new { status = 200, message = "Signature saved" });

        var resource = new SignatureResource(
            FakeHttpMessageHandler.CreateClient(handler),
            request => request.Headers.Add("X-Api-Key", "must-not-leak"));
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("png"));
        await resource.UploadAsync(stream, "access", reuse: true);

        var sent = handler.Requests.Single();
        sent.RequestUri!.PathAndQuery.Should().Be(
            "/v1/signature?signer-access-code=access&type=signature&reuse=true");
        sent.Content!.Headers.ContentType!.MediaType.Should().Be("image/png");
        sent.Headers.Contains("X-Api-Key").Should().BeFalse();
        stream.CanRead.Should().BeTrue();
        stream.Position.Should().Be(3);
    }

    [Fact]
    public async Task Download_GetsSignatureImage()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/signature/signature?signer-access-code=access", "bytes");

        var resource = new SignatureResource(
            FakeHttpMessageHandler.CreateClient(handler),
            request => request.Headers.Add("X-Api-Key", "must-not-leak"));
        var result = await resource.DownloadAsync("access");

        result.Should().NotBeEmpty();
        handler.Requests.Single().Headers.Contains("X-Api-Key").Should().BeFalse();
    }
}
