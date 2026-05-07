using Microsoft.Extensions.DependencyInjection;

namespace Assinafy.Sdk;

/// <summary>
/// <see cref="IServiceCollection"/> extensions for registering <see cref="AssinafyClient"/>
/// with <c>Microsoft.Extensions.Http</c> so the underlying <see cref="HttpClient"/> is
/// managed by <c>IHttpClientFactory</c> (recommended for ASP.NET Core).
/// </summary>
public static class AssinafyServiceCollectionExtensions
{
    private const string HttpClientName = "Assinafy";

    /// <summary>
    /// Register <see cref="AssinafyClient"/> as a singleton, backed by a typed
    /// <see cref="HttpClient"/> from <c>IHttpClientFactory</c>.
    /// </summary>
    public static IHttpClientBuilder AddAssinafy(
        this IServiceCollection services,
        Action<AssinafyClientOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = services.AddHttpClient(HttpClientName);

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
