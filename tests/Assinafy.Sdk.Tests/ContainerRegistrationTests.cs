using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Assinafy.Sdk.Tests;

/// <summary>
/// The SDK ships no container adapter. These tests pin the registration documented in the README
/// so the guidance cannot drift from what actually compiles and runs.
/// </summary>
public sealed class ContainerRegistrationTests
{
    private const string BaseUrl = "https://sandbox.assinafy.com.br/v1";

    private static IServiceCollection RegisterAssinafy(IServiceCollection services) =>
        services
            .AddHttpClient("Assinafy", http =>
            {
                http.BaseAddress = new Uri(BaseUrl + "/");
                http.Timeout = TimeSpan.FromSeconds(30);
            })
            .ConfigurePrimaryHttpMessageHandler(AssinafyClient.CreatePrimaryHandler)
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan)
            .Services
            .AddSingleton(serviceProvider => new AssinafyClient(
                new AssinafyClientOptions { ApiKey = "k", AccountId = "acc", BaseUrl = BaseUrl },
                serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("Assinafy")));

    [Fact]
    public void DocumentedRegistration_ResolvesASingletonWithEveryResource()
    {
        using var provider = RegisterAssinafy(new ServiceCollection()).BuildServiceProvider();

        var first = provider.GetRequiredService<AssinafyClient>();
        var second = provider.GetRequiredService<AssinafyClient>();

        first.Should().BeSameAs(second);
        first.OwnsHttpClient.Should().BeFalse("IHttpClientFactory owns the transport");
        first.Documents.Should().NotBeNull();
        first.Signers.Should().NotBeNull();
        first.Webhooks.Should().NotBeNull();
    }

    [Fact]
    public void CreatePrimaryHandler_DisablesRedirectsSoApiKeysAreNotForwarded()
    {
        using var handler = AssinafyClient.CreatePrimaryHandler();

        handler.AllowAutoRedirect.Should().BeFalse();
        handler.PooledConnectionLifetime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task RegisteredClient_SendsCredentialsWithoutMutatingTheSharedTransport()
    {
        var fakeHandler = new FakeHttpMessageHandler();
        fakeHandler.AddJsonResponse(HttpMethod.Get, "/accounts", FakeHttpMessageHandler.ApiOk(
            new[] { new { id = "acc", name = "Acme", created_at = "2026-01-01" } }));

        var services = new ServiceCollection();
        services.AddHttpClient("Assinafy", http => http.BaseAddress = new Uri(BaseUrl + "/"))
            .ConfigurePrimaryHttpMessageHandler(() => fakeHandler);
        services.AddSingleton(serviceProvider => new AssinafyClient(
            new AssinafyClientOptions { ApiKey = "k", AccountId = "acc", BaseUrl = BaseUrl },
            serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("Assinafy")));

        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<AssinafyClient>();

        var accounts = await client.Accounts.ListAsync(TestContext.Current.CancellationToken);

        accounts.Should().ContainSingle();
        fakeHandler.Requests.Should().ContainSingle()
            .Which.Headers.GetValues("X-Api-Key").Should().Equal("k");

        var factoryClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient("Assinafy");
        factoryClient.DefaultRequestHeaders.Contains("X-Api-Key").Should()
            .BeFalse("credentials are attached per request, never to the shared client");
    }
}
