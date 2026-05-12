namespace Assinafy.Sdk;

/// <summary>
/// Configuration passed to <see cref="AssinafyClient"/>. Provide either
/// <see cref="ApiKey"/> (sent as <c>X-Api-Key</c>) or <see cref="Token"/>
/// (sent as a Bearer authorization header). Most workspace-scoped
/// endpoints also require <see cref="AccountId"/>.
/// </summary>
public sealed class AssinafyClientOptions
{
    /// <summary>Production base URL for the Assinafy API (<c>/v1</c> suffix included).</summary>
    public const string DefaultBaseUrl = "https://api.assinafy.com.br/v1";

    /// <summary>API key sent as the <c>X-Api-Key</c> header. Mutually exclusive with <see cref="Token"/>.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Bearer access token from the login flow. Mutually exclusive with <see cref="ApiKey"/>.</summary>
    public string? Token { get; set; }

    /// <summary>Default workspace account ID, used by account-scoped resources when no override is passed.</summary>
    public string? AccountId { get; set; }

    /// <summary>Base URL for the API. Defaults to <see cref="DefaultBaseUrl"/>.</summary>
    public string BaseUrl { get; set; } = DefaultBaseUrl;

    /// <summary>Per-request timeout for the internally owned <see cref="HttpClient"/>. Ignored when a pre-configured <see cref="HttpClient"/> is supplied.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
