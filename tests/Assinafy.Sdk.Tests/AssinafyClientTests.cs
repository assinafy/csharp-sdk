using System.Text.Json;
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

    [Theory]
    [InlineData("", "acc")]
    [InlineData("key", " ")]
    public void Create_RequiresCredentialsAndAccount(string apiKey, string accountId)
    {
        var act = () => AssinafyClient.Create(apiKey, accountId);

        act.Should().Throw<ArgumentException>();
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
    public void PrimaryHandler_DisablesRedirectsThatCouldForwardApiKeys()
    {
        using var handler = AssinafyClient.CreatePrimaryHandler();

        handler.AllowAutoRedirect.Should().BeFalse();
    }

    [Fact]
    public void OwnedClient_AllowsInfiniteTimeout()
    {
        using var client = new AssinafyClient(new AssinafyClientOptions
        {
            Timeout = Timeout.InfiniteTimeSpan,
        });

        client.OwnsHttpClient.Should().BeTrue();
    }

    [Theory]
    [InlineData("http://api.assinafy.com.br/v1")]
    [InlineData("https://api.assinafy.com.br/custom/v1")]
    [InlineData("https://user@example.com/v1")]
    [InlineData("https://api.assinafy.com.br/v1?target=other")]
    [InlineData("https://api.assinafy.com.br/v1#other")]
    public void BaseUrl_RejectsPlaintextAndAmbiguousDestinations(string baseUrl)
    {
        var act = () => new AssinafyClient(new AssinafyClientOptions { BaseUrl = baseUrl });

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void ExternalHttpClient_RequiresMatchingBaseAddress()
    {
        using var matchingHttp = new HttpClient
        {
            BaseAddress = new Uri("https://sandbox.assinafy.com.br/v1/"),
        };
        using var matchingClient = new AssinafyClient(
            new AssinafyClientOptions { BaseUrl = "https://sandbox.assinafy.com.br/v1" },
            matchingHttp);
        using var mismatchingHttp = new HttpClient
        {
            BaseAddress = new Uri("https://sandbox.assinafy.com.br/v1/"),
        };

        var mismatch = () => new AssinafyClient(new AssinafyClientOptions(), mismatchingHttp);
        mismatch.Should().Throw<ValidationException>();
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
    public async Task UploadAndRequestSignatures_BuildsCollectEntriesFromCreatedSignerIds()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/documents",
            FakeHttpMessageHandler.ApiOk(new { id = "doc-1", status = "metadata_ready" }));
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/signers",
            FakeHttpMessageHandler.ApiOk(new { id = "signer-1", full_name = "Test Signer" }));
        handler.AddJsonResponse(HttpMethod.Post, "/documents/doc-1/assignments",
            FakeHttpMessageHandler.ApiOk(new { id = "assignment-1", method = "collect" }));
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        using var client = new AssinafyClient(
            new AssinafyClientOptions { ApiKey = "key", AccountId = "acc" }, http);
        IReadOnlyList<string>? receivedSignerIds = null;

        var result = await client.UploadAndRequestSignaturesAsync(new UploadAndRequestSignaturesOptions
        {
            FileStream = new MemoryStream([1, 2, 3]),
            FileName = "contract.pdf",
            WaitForReady = false,
            Method = AssignmentMethods.Collect,
            Signers = [new UploadAndRequestSignaturesSigner { FullName = "Test Signer" }],
            EntriesFactory = signerIds =>
            {
                receivedSignerIds = signerIds;
                return
                [
                    new AssignmentEntry
                    {
                        PageId = "page-1",
                        Fields =
                        [
                            new AssignmentEntryField
                            {
                                SignerId = signerIds[0],
                                FieldId = "field-1",
                            },
                        ],
                    },
                ];
            },
        });

        receivedSignerIds.Should().Equal("signer-1");
        result.SignerIds.Should().Equal("signer-1");
        using var body = JsonDocument.Parse(handler.RequestBodies[^1]);
        var entry = body.RootElement.GetProperty("entries").EnumerateArray().Single();
        var field = entry.GetProperty("fields").EnumerateArray().Single();
        field.GetProperty("signer_id").GetString().Should().Be("signer-1");
    }

    [Fact]
    public async Task UploadAndRequestSignatures_RejectsMissingOrAmbiguousCollectEntriesBeforeUploading()
    {
        var handler = new FakeHttpMessageHandler();
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        using var client = new AssinafyClient(
            new AssinafyClientOptions { ApiKey = "key", AccountId = "acc" }, http);

        var missing = () => client.UploadAndRequestSignaturesAsync(new UploadAndRequestSignaturesOptions
        {
            FileStream = new MemoryStream([1]),
            FileName = "contract.pdf",
            Method = AssignmentMethods.Collect,
            Signers = [new UploadAndRequestSignaturesSigner { FullName = "Test Signer" }],
        });
        var ambiguous = () => client.UploadAndRequestSignaturesAsync(new UploadAndRequestSignaturesOptions
        {
            FileStream = new MemoryStream([1]),
            FileName = "contract.pdf",
            Method = AssignmentMethods.Collect,
            Signers = [new UploadAndRequestSignaturesSigner { FullName = "Test Signer" }],
            Entries = [],
            EntriesFactory = _ => [],
        });

        await missing.Should().ThrowAsync<ValidationException>();
        await ambiguous.Should().ThrowAsync<ValidationException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task UploadAndRequestSignatures_RejectsEmptyFactoryResultAfterSignerCreation()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/documents",
            FakeHttpMessageHandler.ApiOk(new { id = "doc-1", status = "metadata_ready" }));
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/signers",
            FakeHttpMessageHandler.ApiOk(new { id = "signer-1", full_name = "Test Signer" }));
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        using var client = new AssinafyClient(
            new AssinafyClientOptions { ApiKey = "key", AccountId = "acc" }, http);

        var act = () => client.UploadAndRequestSignaturesAsync(new UploadAndRequestSignaturesOptions
        {
            FileStream = new MemoryStream([1]),
            FileName = "contract.pdf",
            WaitForReady = false,
            Method = AssignmentMethods.Collect,
            Signers = [new UploadAndRequestSignaturesSigner { FullName = "Test Signer" }],
            EntriesFactory = _ => [],
        });

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*EntriesFactory*");
        handler.Requests.Should().HaveCount(2);
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
