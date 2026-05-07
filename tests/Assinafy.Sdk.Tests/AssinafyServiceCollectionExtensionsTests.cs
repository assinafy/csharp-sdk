using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Assinafy.Sdk.Tests;

public sealed class AssinafyServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAssinafy_RegistersClientAsSingletonBackedByHttpClientFactory()
    {
        var services = new ServiceCollection();
        services.AddAssinafy(o =>
        {
            o.ApiKey = "k";
            o.AccountId = "acc";
        });

        using var provider = services.BuildServiceProvider();
        var client1 = provider.GetRequiredService<AssinafyClient>();
        var client2 = provider.GetRequiredService<AssinafyClient>();

        client1.Should().BeSameAs(client2);
        client1.Documents.Should().NotBeNull();
        client1.Signers.Should().NotBeNull();
    }
}
