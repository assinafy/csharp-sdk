using System.Net.Http.Headers;
using System.Net.Security;
using System.Security.Authentication;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;

namespace Assinafy.Sdk;

/// <summary>
/// Top-level entry point for the Assinafy API. It is thread-safe and holds a pooled
/// <see cref="HttpClient"/>; create one and reuse it for the lifetime of your application,
/// or register it as a singleton in your container.
/// </summary>
public sealed class AssinafyClient : IDisposable
{
    private static readonly string SdkVersion =
        typeof(AssinafyClient).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";

    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;

    internal bool OwnsHttpClient => _ownsHttpClient;

    /// <summary>Authentication and user API-key endpoints.</summary>
    public AuthenticationResource Authentication { get; }

    /// <summary>
    /// OAuth 2.1 authorization-code flow with PKCE, for applications acting in another user's
    /// workspace with that user's permission. Automating your own workspace needs an API key instead.
    /// </summary>
    public OAuthResource OAuth { get; }

    /// <summary>Workspace account management: account CRUD, theme, logo, and document KPIs.</summary>
    public AccountResource Accounts { get; }

    /// <summary>Authenticated-user profile, notification preferences, and cross-account document KPIs.</summary>
    public UserResource Users { get; }

    /// <summary>Document upload, lookup, download, activities, and verification.</summary>
    public DocumentResource Documents { get; }

    /// <summary>Account-scoped signer management and signer self-service endpoints.</summary>
    public SignerResource Signers { get; }

    /// <summary>Signature assignment creation, cost estimation, resend, and expiration.</summary>
    public AssignmentResource Assignments { get; }

    /// <summary>Template upload, listing, update, deletion, details, and page downloads.</summary>
    public TemplateResource Templates { get; }

    /// <summary>Workspace tag management and document tag attachment.</summary>
    public TagResource Tags { get; }

    /// <summary>Field definition CRUD and value validation.</summary>
    public FieldResource Fields { get; }

    /// <summary>Public document lookup and signer token delivery.</summary>
    public PublicDocumentResource PublicDocuments { get; }

    /// <summary>Signer-facing document access, signing, declining, certificate signing, and public downloads.</summary>
    public SigningResource Signing { get; }

    /// <summary>Signer signature/initial image upload and download.</summary>
    public SignatureResource Signatures { get; }

    /// <summary>Webhook subscription configuration, event catalog, and dispatch history.</summary>
    public WebhookResource Webhooks { get; }

    /// <summary>Create a client with an internally owned <see cref="HttpClient"/>.</summary>
    /// <param name="options">Authentication, account, base URL, and timeout configuration.</param>
    public AssinafyClient(AssinafyClientOptions options)
        : this(options, new HttpClient(CreatePrimaryHandler()), ownsHttpClient: true) { }

    /// <summary>
    /// Create a client backed by a caller-supplied <see cref="HttpClient"/>.
    /// The caller is responsible for the lifetime of <paramref name="http"/>;
    /// this constructor will not dispose it. Use this overload to register the client with
    /// <c>IHttpClientFactory</c> in an ASP.NET Core container.
    /// Authentication is attached per request, so the supplied client's default
    /// headers are not mutated with credentials. Its <see cref="HttpClient.BaseAddress"/>
    /// must match <see cref="AssinafyClientOptions.BaseUrl"/>. When using an API key,
    /// configure the supplied primary handler with automatic redirects disabled —
    /// <see cref="CreatePrimaryHandler"/> does this — because .NET otherwise forwards
    /// custom headers such as <c>X-Api-Key</c> to redirect targets.
    /// </summary>
    /// <param name="options">Authentication, account, and base URL configuration. Its timeout is ignored for a supplied client.</param>
    /// <param name="http">Caller-owned HTTP client whose existing base address, if set, matches <paramref name="options"/>.</param>
    public AssinafyClient(AssinafyClientOptions options, HttpClient http)
        : this(options, http ?? throw new ArgumentNullException(nameof(http)), ownsHttpClient: false) { }

    internal AssinafyClient(AssinafyClientOptions options, HttpClient http, bool ownsHttpClient)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _ownsHttpClient = ownsHttpClient;

        try
        {
            ArgumentNullException.ThrowIfNull(options);

            if (!string.IsNullOrWhiteSpace(options.ApiKey) && !string.IsNullOrWhiteSpace(options.Token))
                throw new ValidationException(
                    "ApiKey and Token are mutually exclusive; provide exactly one (or neither for public-only access).");

            var authenticate = BuildAuthenticator(options);
            ConfigureHttpClient(_http, options, applyTimeout: ownsHttpClient);

            Authentication = new AuthenticationResource(_http, authenticate);
            OAuth = new OAuthResource(_http, authenticate);
            Accounts = new AccountResource(_http, options.AccountId, authenticate);
            Users = new UserResource(_http, authenticate);
            Documents = new DocumentResource(_http, options.AccountId, authenticate);
            Signers = new SignerResource(_http, options.AccountId, authenticate);
            Assignments = new AssignmentResource(_http, options.AccountId, authenticate);
            Templates = new TemplateResource(_http, options.AccountId, authenticate);
            Tags = new TagResource(_http, options.AccountId, authenticate);
            Fields = new FieldResource(_http, options.AccountId, authenticate);
            PublicDocuments = new PublicDocumentResource(_http, authenticate);
            Signing = new SigningResource(_http, authenticate);
            Signatures = new SignatureResource(_http, authenticate);
            Webhooks = new WebhookResource(_http, options.AccountId, authenticate);
        }
        catch
        {
            if (_ownsHttpClient)
                _http.Dispose();
            throw;
        }
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
            if (apiKey.Any(char.IsControl))
                throw new ValidationException("ApiKey must not contain control characters.");

            return request => request.Headers.Add("X-Api-Key", apiKey);
        }

        if (!string.IsNullOrWhiteSpace(options.Token))
        {
            var token = options.Token;
            var paddingIndex = token.IndexOf('=');
            var tokenBody = paddingIndex < 0 ? token : token[..paddingIndex];
            if (tokenBody.Length == 0 ||
                tokenBody.Any(character =>
                    !char.IsAsciiLetterOrDigit(character) &&
                    character is not ('-' or '.' or '_' or '~' or '+' or '/')) ||
                paddingIndex >= 0 && token.Skip(paddingIndex).Any(character => character != '='))
                throw new ValidationException("Token is not a valid Bearer credential.");

            AuthenticationHeaderValue authorization;
            try
            {
                authorization = new AuthenticationHeaderValue("Bearer", token);
            }
            catch (FormatException ex)
            {
                throw new ValidationException("Token is not a valid Bearer credential.", null, ex);
            }

            return request => request.Headers.Authorization = authorization;
        }

        return null;
    }

    private static void ConfigureHttpClient(HttpClient http, AssinafyClientOptions options, bool applyTimeout)
    {
        var configuredBaseUrl = (string.IsNullOrWhiteSpace(options.BaseUrl)
            ? AssinafyClientOptions.DefaultBaseUrl
            : options.BaseUrl).Trim();
        var baseAddress = ValidateBaseAddress(configuredBaseUrl);

        if (http.BaseAddress is not null && http.BaseAddress != baseAddress)
            throw new ValidationException(
                "The supplied HttpClient BaseAddress must match AssinafyClientOptions.BaseUrl.");

        http.BaseAddress ??= baseAddress;

        if (applyTimeout)
        {
            if (options.Timeout <= TimeSpan.Zero && options.Timeout != Timeout.InfiniteTimeSpan)
                throw new ValidationException("Timeout must be greater than zero or infinite.");

            http.Timeout = options.Timeout;
        }

        var headers = http.DefaultRequestHeaders;
        if (!headers.Accept.Any(a => string.Equals(a.MediaType, "application/json", StringComparison.OrdinalIgnoreCase)))
            headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!headers.UserAgent.Any(h => h.Product?.Name == "assinafy-csharp-sdk"))
            headers.UserAgent.Add(new ProductInfoHeaderValue("assinafy-csharp-sdk", SdkVersion));
    }

    /// <summary>
    /// Create the primary handler the SDK uses for its own transport: automatic redirects disabled
    /// (so <c>X-Api-Key</c> is never forwarded to a redirect target), a five-minute pooled
    /// connection lifetime (so a long-lived client still picks up DNS changes), and TLS 1.2 or
    /// 1.3 only (TLS 1.0 and 1.1 are refused).
    /// </summary>
    /// <remarks>
    /// Pass this to <c>ConfigurePrimaryHttpMessageHandler</c> when registering the client with
    /// <c>IHttpClientFactory</c>, or use it directly when constructing your own
    /// <see cref="HttpClient"/> for the <see cref="AssinafyClient(AssinafyClientOptions, HttpClient)"/>
    /// overload, to get the same protections as an SDK-owned transport.
    /// </remarks>
    /// <returns>A handler configured for safe credential handling on a long-lived client.</returns>
    public static SocketsHttpHandler CreatePrimaryHandler() => new()
    {
        AllowAutoRedirect = false,
        PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        SslOptions = new SslClientAuthenticationOptions
        {
            EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
        },
    };

    private static Uri ValidateBaseAddress(string value)
    {
        if (!Uri.TryCreate(value.TrimEnd('/') + "/", UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            uri.AbsolutePath != "/v1/")
            throw new ValidationException(
                "BaseUrl must be an absolute HTTPS URL whose path is exactly /v1 and has no user info, query, or fragment.");

        return uri;
    }

    /// <summary>Convenience factory that creates an API-key client and optionally tweaks options.</summary>
    /// <param name="apiKey">API key sent in the <c>X-Api-Key</c> header.</param>
    /// <param name="accountId">Default workspace account ID for account-scoped resources.</param>
    /// <param name="configure">Optional callback that can change the generated client options.</param>
    /// <returns>A configured Assinafy client.</returns>
    public static AssinafyClient Create(
        string apiKey,
        string accountId,
        Action<AssinafyClientOptions>? configure = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountId);
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
    /// <param name="config">Configuration keys and values used to construct the client options.</param>
    /// <returns>A client configured from the supplied key-value settings.</returns>
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
    /// create signers, and create an assignment in a single call.
    /// </summary>
    /// <remarks>
    /// The API has no transaction spanning these calls. If a later request fails, earlier documents
    /// and signers remain available to inspect or delete; the SDK does not destroy them automatically.
    /// For collect assignments whose entries reference signers created by this helper, use
    /// <see cref="UploadAndRequestSignaturesOptions.EntriesFactory"/>; it runs after all signers are created.
    /// </remarks>
    /// <param name="options">PDF, signer, assignment, and optional account configuration for the operation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created document, assignment, and signer identifiers.</returns>
    public async Task<UploadAndRequestSignaturesResult> UploadAndRequestSignaturesAsync(
        UploadAndRequestSignaturesOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(options.FileStream);
        ArgumentException.ThrowIfNullOrWhiteSpace(options.FileName);
        if (options.Signers is null || options.Signers.Count == 0)
            throw new ValidationException("At least one signer is required.");
        if (options.Signers.Any(signer => signer is null || string.IsNullOrWhiteSpace(signer.FullName)))
            throw new ValidationException("Every signer must have a full name.");
        if (options.Entries is not null && options.EntriesFactory is not null)
            throw new ValidationException("Specify either Entries or EntriesFactory, not both.");
        if (string.Equals(options.Method, AssignmentMethods.Collect, StringComparison.OrdinalIgnoreCase) &&
            options.Entries is not { Count: > 0 } && options.EntriesFactory is null)
            throw new ValidationException("Collect assignments require field entries.");

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

            if (string.IsNullOrWhiteSpace(created.Id))
                throw new SerializationException("The API created a signer without returning its ID.");

            signerIds.Add(created.Id);
            signerRefs.Add(new SignerRef
            {
                Id = created.Id,
                VerificationMethod = signer.VerificationMethod,
                NotificationMethods = signer.NotificationMethods,
                Step = signer.Step,
            });
        }

        IReadOnlyList<AssignmentEntry>? entries = options.Entries;
        if (options.EntriesFactory is not null)
        {
            entries = options.EntriesFactory(signerIds);
            if (entries is not { Count: > 0 })
                throw new ValidationException("EntriesFactory must return at least one field entry.");
        }

        var assignment = await Assignments.CreateAsync(
            document.Id,
            new CreateAssignmentRequest
            {
                Method = options.Method ?? AssignmentMethods.Virtual,
                Signers = signerRefs,
                Message = options.Message,
                ExpiresAt = options.ExpiresAt,
                CopyReceivers = options.CopyReceivers,
                Entries = entries,
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
