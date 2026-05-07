using System.Net.Http.Headers;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;

namespace Assinafy.Sdk;

/// <summary>
/// Top-level entry point for the Assinafy API. Reuse this client for the lifetime
/// of your application, or register it through <c>services.AddAssinafy(...)</c>.
/// </summary>
public sealed class AssinafyClient : IDisposable
{
    private static readonly string SdkVersion =
        typeof(AssinafyClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;

    public AuthenticationResource Authentication { get; }
    public DocumentResource Documents { get; }
    public SignerResource Signers { get; }
    public AssignmentResource Assignments { get; }
    public TemplateResource Templates { get; }
    public FieldResource Fields { get; }
    public PublicDocumentResource PublicDocuments { get; }
    public SigningResource Signing { get; }
    public SignatureResource Signatures { get; }
    public WebhookResource Webhooks { get; }

    public AssinafyClient(AssinafyClientOptions options)
        : this(options, new HttpClient(), ownsHttpClient: true) { }

    public AssinafyClient(AssinafyClientOptions options, HttpClient http)
        : this(options, http ?? throw new ArgumentNullException(nameof(http)), ownsHttpClient: false) { }

    private AssinafyClient(AssinafyClientOptions options, HttpClient http, bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(options);

        _http = http;
        _ownsHttpClient = ownsHttpClient;

        ConfigureHttpClient(_http, options, applyTimeout: ownsHttpClient);

        Authentication = new AuthenticationResource(_http);
        Documents = new DocumentResource(_http, options.AccountId);
        Signers = new SignerResource(_http, options.AccountId);
        Assignments = new AssignmentResource(_http);
        Templates = new TemplateResource(_http, options.AccountId);
        Fields = new FieldResource(_http, options.AccountId);
        PublicDocuments = new PublicDocumentResource(_http);
        Signing = new SigningResource(_http);
        Signatures = new SignatureResource(_http);
        Webhooks = new WebhookResource(_http, options.AccountId);
    }

    private static void ConfigureHttpClient(HttpClient http, AssinafyClientOptions options, bool applyTimeout)
    {
        if (options.Timeout <= TimeSpan.Zero)
            throw new ValidationException("Timeout must be greater than zero.");

        var baseUrl = (string.IsNullOrWhiteSpace(options.BaseUrl)
            ? AssinafyClientOptions.DefaultBaseUrl
            : options.BaseUrl).TrimEnd('/') + "/";

        http.BaseAddress ??= new Uri(baseUrl, UriKind.Absolute);

        if (applyTimeout)
            http.Timeout = options.Timeout;

        var headers = http.DefaultRequestHeaders;
        if (!headers.Accept.Any(a => string.Equals(a.MediaType, "application/json", StringComparison.OrdinalIgnoreCase)))
            headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!headers.UserAgent.Any(h => h.Product?.Name == "assinafy-csharp-sdk"))
            headers.UserAgent.Add(new ProductInfoHeaderValue("assinafy-csharp-sdk", SdkVersion));

        headers.Remove("X-Api-Key");
        headers.Authorization = null;

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
            headers.Add("X-Api-Key", options.ApiKey);
        else if (!string.IsNullOrWhiteSpace(options.Token))
            headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
    }

    public static AssinafyClient Create(
        string apiKey,
        string accountId,
        Action<AssinafyClientOptions>? configure = null)
    {
        var options = new AssinafyClientOptions { ApiKey = apiKey, AccountId = accountId };
        configure?.Invoke(options);
        return new AssinafyClient(options);
    }

    public static AssinafyClient FromConfig(IDictionary<string, string?> config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var options = new AssinafyClientOptions
        {
            ApiKey = TryGet(config, "api_key", "apiKey"),
            AccountId = TryGet(config, "account_id", "accountId"),
        };

        if (string.IsNullOrWhiteSpace(options.ApiKey))
            options.Token = TryGet(config, "token", "access_token", "accessToken");

        var baseUrl = TryGet(config, "base_url", "baseUrl");
        if (!string.IsNullOrWhiteSpace(baseUrl))
            options.BaseUrl = baseUrl;

        return new AssinafyClient(options);

        static string? TryGet(IDictionary<string, string?> c, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (c.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return null;
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
            _http.Dispose();
    }

    /// <summary>
    /// Convenience helper: upload a PDF, optionally wait for it to be ready,
    /// create signers, and create a virtual assignment in a single call.
    /// </summary>
    public async Task<UploadAndRequestSignaturesResult> UploadAndRequestSignaturesAsync(
        UploadAndRequestSignaturesOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.Signers is null || options.Signers.Count == 0)
            throw new ValidationException("At least one signer is required.");

        var document = await Documents.UploadAsync(
            options.FileStream,
            options.FileName,
            options.AccountId,
            cancellationToken).ConfigureAwait(false);

        if (options.WaitForReady ?? true)
            await Documents.WaitUntilReadyAsync(document.Id, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

        var signerIds = new List<string>(options.Signers.Count);
        foreach (var signer in options.Signers)
        {
            var created = await Signers.CreateAsync(
                new CreateSignerRequest
                {
                    FullName = signer.FullName,
                    Email = signer.Email,
                    WhatsAppPhoneNumber = signer.WhatsAppPhoneNumber,
                },
                options.AccountId,
                cancellationToken).ConfigureAwait(false);

            signerIds.Add(created.Id);
        }

        var assignment = await Assignments.CreateAsync(
            document.Id,
            new CreateAssignmentRequest
            {
                Method = "virtual",
                Signers = signerIds.Select(id => (SignerRef)id).ToList(),
                Message = options.Message,
                ExpiresAt = options.ExpiresAt,
                CopyReceivers = options.CopyReceivers,
            },
            cancellationToken).ConfigureAwait(false);

        return new UploadAndRequestSignaturesResult
        {
            Document = document,
            Assignment = assignment,
            SignerIds = signerIds,
        };
    }
}
