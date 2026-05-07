namespace Assinafy.Sdk;

public sealed class AssinafyClientOptions
{
    public const string DefaultBaseUrl = "https://api.assinafy.com.br/v1";

    public string? ApiKey { get; set; }
    public string? Token { get; set; }
    public string? AccountId { get; set; }
    public string BaseUrl { get; set; } = DefaultBaseUrl;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
