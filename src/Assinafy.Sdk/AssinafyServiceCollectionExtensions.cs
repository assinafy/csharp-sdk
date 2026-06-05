using Microsoft.Extensions.DependencyInjection;

namespace Assinafy.Sdk;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for registering <see cref="AssinafyClient"/>
/// with <c>Microsoft.Extensions.Http</c>. The underlying <see cref="HttpClient"/> is created
/// from <c>IHttpClientFactory</c> and captured by a singleton <see cref="AssinafyClient"/>.
/// Because the client is long-lived, connection/DNS recycling is governed by a
/// <see cref="SocketsHttpHandler.PooledConnectionLifetime"/> on the primary handler rather
/// than by <c>IHttpClientFactory</c> handler rotation (which a captured client cannot observe).
/// </summary>
public static class AssinafyServiceCollectionExtensions
{
    private const string HttpClientName = "Assinafy";

    /// <summary>
    /// Register <see cref="AssinafyClient"/> as a singleton, backed by a dedicated named
    /// <see cref="HttpClient"/> from <c>IHttpClientFactory</c>. The returned
    /// <see cref="IHttpClientBuilder"/> can be used to chain Polly handlers, custom message
    /// handlers, etc.
    /// </summary>
    public static IHttpClientBuilder AddAssinafy(
        this IServiceCollection services,
        Action<AssinafyClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = services
            .AddHttpClient(HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            })
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

        services.AddSingleton(sp =>
        {
            var options = new AssinafyClientOptions();
            configure(options);
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var http = factory.CreateClient(HttpClientName);
            return new AssinafyClient(options, http);
        });

        return builder;
    }
}
