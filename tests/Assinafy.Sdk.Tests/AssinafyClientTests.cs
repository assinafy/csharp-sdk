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
    public void ApiKey_SetsXApiKeyHeaderOnExternalHttpClient()
    {
        using var http = new HttpClient();
        using var client = new AssinafyClient(
            new AssinafyClientOptions { ApiKey = "my-key", AccountId = "acc" }, http);

        http.DefaultRequestHeaders.GetValues("X-Api-Key").Should().Contain("my-key");
    }

    [Fact]
    public void Token_SetsBearerAuthorizationHeaderOnExternalHttpClient()
    {
        using var http = new HttpClient();
        using var client = new AssinafyClient(
            new AssinafyClientOptions { Token = "legacy", AccountId = "acc" }, http);

        http.DefaultRequestHeaders.Authorization!.Scheme.Should().Be("Bearer");
        http.DefaultRequestHeaders.Authorization!.Parameter.Should().Be("legacy");
    }

    [Fact]
    public void DefaultBaseUrl_IsProductionApiV1()
    {
        using var http = new HttpClient();
        using var client = new AssinafyClient(new AssinafyClientOptions { ApiKey = "k" }, http);

        http.BaseAddress!.ToString().Should().Be("https://api.assinafy.com.br/v1/");
    }

    [Fact]
    public void ExternalHttpClient_IsNotDisposedByClient()
    {
        var http = new HttpClient();
        var client = new AssinafyClient(new AssinafyClientOptions { ApiKey = "k" }, http);

        client.Dispose();
        var act = () => http.DefaultRequestHeaders.GetValues("X-Api-Key");

        act.Should().NotThrow();
        http.Dispose();
    }
}
