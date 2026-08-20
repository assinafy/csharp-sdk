using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests;

public sealed class AssinafyClientTests
{
    [Fact]
    public void Constructor_AllowsPublicOnlyClient()
    {
        using var client = new AssinafyClient(new AssinafyClientOptions());
        client.PublicDocuments.Should().NotBeNull();
        client.Signing.Should().NotBeNull();
        client.Signatures.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_InitializesDocumentedResources()
    {
        using var client = new AssinafyClient(new AssinafyClientOptions { ApiKey = "k", AccountId = "acc" });

        client.Authentication.Should().NotBeNull();
        client.Accounts.Should().NotBeNull();
        client.Users.Should().NotBeNull();
        client.Documents.Should().NotBeNull();
        client.Signers.Should().NotBeNull();
        client.Assignments.Should().NotBeNull();
        client.Templates.Should().NotBeNull();
        client.Fields.Should().NotBeNull();
        client.PublicDocuments.Should().NotBeNull();
        client.Signing.Should().NotBeNull();
        client.Signatures.Should().NotBeNull();
        client.Webhooks.Should().NotBeNull();
    }

    [Fact]
    public void Create_BuildsConfiguredClient()
    {
        using var client = AssinafyClient.Create("k", "acc");
        client.Documents.Should().NotBeNull();
    }

    [Fact]
    public void FromConfig_AcceptsSnakeCaseKeys()
    {
        using var client = AssinafyClient.FromConfig(new Dictionary<string, string?>
        {
            ["api_key"] = "k",
            ["account_id"] = "acc",
        });

        client.Documents.Should().NotBeNull();
    }

    [Fact]
    public void FromConfig_AcceptsCamelCaseKeys()
    {
        using var client = AssinafyClient.FromConfig(new Dictionary<string, string?>
        {
            ["apiKey"] = "k",
            ["accountId"] = "acc",
        });

        client.Documents.Should().NotBeNull();
    }

    [Fact]
    public async Task ApiKey_AttachesXApiKeyPerRequestWithoutMutatingClient()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/statuses",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        using var client = new AssinafyClient(
            new AssinafyClientOptions { ApiKey = "my-key", AccountId = "acc" }, http);

        await client.Documents.ListStatusesAsync();

        handler.Requests.Last().Headers.GetValues("X-Api-Key").Should().Contain("my-key");
        // The caller-supplied client's shared headers must not be mutated with credentials.
        http.DefaultRequestHeaders.Contains("X-Api-Key").Should().BeFalse();
    }

    [Fact]
    public async Task Token_AttachesBearerAuthorizationPerRequest()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/statuses",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        using var client = new AssinafyClient(
            new AssinafyClientOptions { Token = "legacy", AccountId = "acc" }, http);

        await client.Documents.ListStatusesAsync();

        var auth = handler.Requests.Last().Headers.Authorization!;
        auth.Scheme.Should().Be("Bearer");
        auth.Parameter.Should().Be("legacy");
        http.DefaultRequestHeaders.Authorization.Should().BeNull();
    }

    [Fact]
    public void MutuallyExclusiveCredentials_Throw()
    {
        var act = () => new AssinafyClient(
            new AssinafyClientOptions { ApiKey = "k", Token = "t", AccountId = "acc" });

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void DefaultBaseUrl_IsProductionApiV1()
    {
        using var http = new HttpClient();
        using var client = new AssinafyClient(new AssinafyClientOptions { ApiKey = "k" }, http);

        http.BaseAddress!.ToString().Should().Be("https://api.assinafy.com.br/v1/");
    }

    [Fact]
    public async Task ExternalHttpClient_IsNotDisposedByClient()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/statuses",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));
        var http = FakeHttpMessageHandler.CreateClient(handler);
        var client = new AssinafyClient(new AssinafyClientOptions { ApiKey = "k", AccountId = "acc" }, http);

        client.Dispose();

        // Disposing the SDK client must not dispose a caller-supplied HttpClient: it stays usable.
        var act = async () => await client.Documents.ListStatusesAsync();
        await act.Should().NotThrowAsync();
        http.Dispose();
    }

    [Fact]
    public async Task UploadAndRequestSignatures_CompletesDocumentSignerAssignmentFlow()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/documents",
            FakeHttpMessageHandler.ApiOk(new { id = "doc-1", status = "metadata_ready" }));
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/signers",
            FakeHttpMessageHandler.ApiOk(new { id = "signer-1", full_name = "Test Signer" }));
        handler.AddJsonResponse(HttpMethod.Post, "/documents/doc-1/assignments",
            FakeHttpMessageHandler.ApiOk(new { id = "assignment-1", method = "virtual" }));
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        using var client = new AssinafyClient(
            new AssinafyClientOptions { ApiKey = "key", AccountId = "acc" }, http);
        using var stream = new MemoryStream([1, 2, 3]);

        var result = await client.UploadAndRequestSignaturesAsync(new UploadAndRequestSignaturesOptions
        {
            FileStream = stream,
            FileName = "contract.pdf",
            WaitForReady = false,
            Signers = [new UploadAndRequestSignaturesSigner { FullName = "Test Signer" }],
        });

        result.Document.Id.Should().Be("doc-1");
        result.SignerIds.Should().Equal("signer-1");
        result.Assignment.Id.Should().Be("assignment-1");
        handler.Requests.Select(request => request.Method)
            .Should().Equal(HttpMethod.Post, HttpMethod.Post, HttpMethod.Post);
    }

    [Fact]
    public async Task UploadAndRequestSignatures_ValidatesAllSignersBeforeUploading()
    {
        var handler = new FakeHttpMessageHandler();
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        using var client = new AssinafyClient(
            new AssinafyClientOptions { ApiKey = "key", AccountId = "acc" }, http);

        await ((Func<Task>)(() => client.UploadAndRequestSignaturesAsync(
            new UploadAndRequestSignaturesOptions
            {
                FileStream = new MemoryStream([1]),
                FileName = "contract.pdf",
                Signers = [new UploadAndRequestSignaturesSigner { FullName = "" }],
            }))).Should().ThrowAsync<ValidationException>();

        handler.Requests.Should().BeEmpty();
    }
}
