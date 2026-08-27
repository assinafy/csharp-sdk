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
    /// <param name="services">Service collection that receives the SDK registrations.</param>
    /// <param name="configure">Callback that configures authentication, account, base URL, and timeout options.</param>
    /// <returns>The named HTTP-client builder for additional configuration.</returns>
    public static IHttpClientBuilder AddAssinafy(
        this IServiceCollection services,
        Action<AssinafyClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // Build the options once at registration so the configured Timeout can be applied to the
        // named HttpClient (a caller-supplied/factory client is not mutated by AssinafyClient, so the
        // Timeout would otherwise be silently ignored on this path).
        var options = new AssinafyClientOptions();
        configure(options);

        var builder = services
            .AddHttpClient(HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(AssinafyClient.CreatePrimaryHandler)
            .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

        if (options.Timeout > TimeSpan.Zero || options.Timeout == Timeout.InfiniteTimeSpan)
            builder.ConfigureHttpClient(http => http.Timeout = options.Timeout);

        services.AddSingleton(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var http = factory.CreateClient(HttpClientName);
            return new AssinafyClient(options, http, ownsHttpClient: true);
        });

        return builder;
    }
}
