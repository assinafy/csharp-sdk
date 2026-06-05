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

    /// <summary>Authentication and user API-key endpoints.</summary>
    public AuthenticationResource Authentication { get; }

    /// <summary>Document upload, lookup, download, activities, and verification.</summary>
    public DocumentResource Documents { get; }

    /// <summary>Account-scoped signer management and signer self-service endpoints.</summary>
    public SignerResource Signers { get; }

    /// <summary>Signature assignment creation, cost estimation, resend, and expiration.</summary>
    public AssignmentResource Assignments { get; }

    /// <summary>Template listing and detail lookup.</summary>
    public TemplateResource Templates { get; }

    /// <summary>Workspace tag management and document tag attachment.</summary>
    public TagResource Tags { get; }

    /// <summary>Field definition CRUD and value validation.</summary>
    public FieldResource Fields { get; }

    /// <summary>Public document lookup and signer token delivery.</summary>
    public PublicDocumentResource PublicDocuments { get; }

    /// <summary>Signer-facing signing flow: get assignment, sign, decline.</summary>
    public SigningResource Signing { get; }

    /// <summary>Signer signature/initial image upload and download.</summary>
    public SignatureResource Signatures { get; }

    /// <summary>Webhook subscription configuration, event catalog, and dispatch history.</summary>
    public WebhookResource Webhooks { get; }

    /// <summary>Create a client with an internally owned <see cref="HttpClient"/>.</summary>
    public AssinafyClient(AssinafyClientOptions options)
        : this(options, new HttpClient(), ownsHttpClient: true) { }

    /// <summary>
    /// Create a client backed by a caller-supplied <see cref="HttpClient"/>.
    /// The caller is responsible for the lifetime of <paramref name="http"/>;
    /// this constructor will not dispose it. Preferred for ASP.NET Core via
    /// <see cref="AssinafyServiceCollectionExtensions.AddAssinafy"/>.
    /// Authentication is attached per request, so the supplied client's default
    /// headers are not mutated with credentials and the client stays safe to reuse;
    /// only <c>Accept</c>, <c>User-Agent</c>, and an unset <c>BaseAddress</c> are configured.
    /// </summary>
    public AssinafyClient(AssinafyClientOptions options, HttpClient http)
        : this(options, http ?? throw new ArgumentNullException(nameof(http)), ownsHttpClient: false) { }

    private AssinafyClient(AssinafyClientOptions options, HttpClient http, bool ownsHttpClient)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrWhiteSpace(options.ApiKey) && !string.IsNullOrWhiteSpace(options.Token))
            throw new ValidationException(
                "ApiKey and Token are mutually exclusive; provide exactly one (or neither for public-only access).");

        _http = http;
        _ownsHttpClient = ownsHttpClient;

        ConfigureHttpClient(_http, options, applyTimeout: ownsHttpClient);

        var authenticate = BuildAuthenticator(options);

        Authentication = new AuthenticationResource(_http, authenticate);
        Documents = new DocumentResource(_http, options.AccountId, authenticate);
        Signers = new SignerResource(_http, options.AccountId, authenticate);
        Assignments = new AssignmentResource(_http, authenticate);
        Templates = new TemplateResource(_http, options.AccountId, authenticate);
        Tags = new TagResource(_http, options.AccountId, authenticate);
        Fields = new FieldResource(_http, options.AccountId, authenticate);
        PublicDocuments = new PublicDocumentResource(_http, authenticate);
        Signing = new SigningResource(_http, authenticate);
        Signatures = new SignatureResource(_http, authenticate);
        Webhooks = new WebhookResource(_http, options.AccountId, authenticate);
    }

    /// <summary>
    /// Build the per-request authentication step. Auth is applied to each outgoing
    /// <see cref="HttpRequestMessage"/> rather than to the <see cref="HttpClient"/>'s shared
    /// default headers, so a caller-supplied client is never mutated and is safe to reuse.
    /// </summary>
    private static Action<HttpRequestMessage>? BuildAuthenticator(AssinafyClientOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            var apiKey = options.ApiKey;
            return request => request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
        }

        if (!string.IsNullOrWhiteSpace(options.Token))
        {
            var token = options.Token;
            return request => request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return null;
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
    }

    /// <summary>Convenience factory that creates an API-key client and optionally tweaks options.</summary>
    public static AssinafyClient Create(
        string apiKey,
        string accountId,
        Action<AssinafyClientOptions>? configure = null)
    {
        var options = new AssinafyClientOptions { ApiKey = apiKey, AccountId = accountId };
        configure?.Invoke(options);
        return new AssinafyClient(options);
    }

    /// <summary>
    /// Build a client from a dictionary of configuration keys. Accepts both
    /// snake_case (<c>api_key</c>, <c>account_id</c>) and camelCase (<c>apiKey</c>,
    /// <c>accountId</c>) variants, plus <c>token</c>/<c>access_token</c>/<c>accessToken</c>
    /// when no API key is provided, and an optional <c>base_url</c>/<c>baseUrl</c>.
    /// </summary>
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

    /// <summary>Disposes the underlying <see cref="HttpClient"/> only if it was created internally.</summary>
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
        var signerRefs = new List<SignerRef>(options.Signers.Count);
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
            signerRefs.Add(new SignerRef
            {
                Id = created.Id,
                VerificationMethod = signer.VerificationMethod,
                NotificationMethods = signer.NotificationMethods,
                Step = signer.Step,
            });
        }

        var assignment = await Assignments.CreateAsync(
            document.Id,
            new CreateAssignmentRequest
            {
                Method = options.Method ?? "virtual",
                Signers = signerRefs,
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
