# Assinafy .NET SDK

*[Leia em português](README.md) · English*

A typed .NET client for the [Assinafy](https://api.assinafy.com.br/v1/docs) electronic-signature
API. It covers the complete documented HTTP surface — documents, templates, signers, assignments,
the signer-facing signing flow, signature images, tags, fields, webhooks, accounts, users, and the
OAuth 2.1 authorization-code flow — as strongly-typed resources on a single `AssinafyClient`, with
one exception hierarchy, envelope handling, and pagination already taken care of.

Targets `net8.0`, `net9.0`, and `net10.0`.

## Contents

1. [Installation](#installation)
2. [Credentials and environments](#credentials-and-environments)
3. [Creating a client](#creating-a-client)
4. [OAuth 2.1 for multi-workspace apps](#oauth-21-for-multi-workspace-apps)
5. [Dependency injection](#dependency-injection)
6. [How requests and responses work](#how-requests-and-responses-work)
7. [Error handling](#error-handling)
8. [The signature lifecycle](#the-signature-lifecycle)
9. [Documents](#documents)
10. [Templates](#templates)
11. [Signers](#signers)
12. [Assignments](#assignments)
13. [The signer-facing flow](#the-signer-facing-flow)
14. [Signature images](#signature-images)
15. [Public documents](#public-documents)
16. [Tags](#tags)
17. [Fields](#fields)
18. [Webhooks](#webhooks)
19. [Accounts and users](#accounts-and-users)
20. [Reference tables](#reference-tables)
21. [Testing](#testing)
22. [Support matrix and versioning](#support-matrix-and-versioning)
23. [Further reading](#further-reading)

## Installation

```bash
dotnet add package Assinafy.Sdk --version 2.3.0
```

Applications need a runtime compatible with `net8.0`, `net9.0`, or `net10.0`. Contributors need
.NET SDKs 8.0.424, 9.0.317, and 10.0.400; [`global.json`](global.json) selects .NET 10 for
repository commands.

## Credentials and environments

Assinafy accepts either credential:

| Credential | Sent as | Obtained from | Use for |
|---|---|---|---|
| API key | `X-Api-Key` header | `Authentication.CreateApiKeyAsync` (or the web app) | Server-to-server integrations in **your own** workspace |
| Access token | `Authorization: Bearer …` | `Authentication.LoginAsync` / `SocialLoginAsync` | Acting as a signed-in user |
| OAuth token | `Authorization: Bearer …` | `OAuth.ExchangeCodeAsync` | An app acting in **someone else's** workspace, with their permission |

The first two are mutually exclusive; supplying both throws a `ValidationException`. An OAuth
access token is supplied as `Token`, exactly like any other bearer credential — see
[OAuth 2.1 for multi-workspace apps](#oauth-21-for-multi-workspace-apps). Create a *separate*
user for an API-key integration so it can be granted only the access it needs, and never commit the
key.

Develop against the sandbox, then switch a single option to go live:

| Environment | API base URL | Web app |
|---|---|---|
| Sandbox | `https://sandbox.assinafy.com.br/v1` | `https://app-sandbox.assinafy.com.br` |
| Production (default) | `https://api.assinafy.com.br/v1` | `https://app.assinafy.com.br` |

Most endpoints are scoped to a workspace **account**. Set `AccountId` once on the client and every
account-scoped method uses it; each of those methods also takes an optional `accountId` override.
`Accounts.ListAsync()` discovers the IDs available to the current credential.

## Creating a client

```csharp
using Assinafy.Sdk;
using Assinafy.Sdk.Models;

using var client = new AssinafyClient(new AssinafyClientOptions
{
    ApiKey = Environment.GetEnvironmentVariable("ASSINAFY_API_KEY"),
    AccountId = Environment.GetEnvironmentVariable("ASSINAFY_ACCOUNT_ID"),
    BaseUrl = "https://sandbox.assinafy.com.br/v1",   // omit for production
    Timeout = TimeSpan.FromSeconds(30),               // default
});
```

Two shorthands exist for common cases:

```csharp
using var fromArgs = AssinafyClient.Create(apiKey, accountId);

using var fromSettings = AssinafyClient.FromConfig(new Dictionary<string, string?>
{
    ["api_key"] = configuration["Assinafy:ApiKey"],
    ["account_id"] = configuration["Assinafy:AccountId"],
});
```

`FromConfig` accepts snake_case or camelCase keys (`api_key`/`apiKey`, `account_id`/`accountId`,
`token`/`access_token`/`accessToken`, `base_url`/`baseUrl`).

**Lifetime.** `AssinafyClient` is thread-safe and holds a pooled `HttpClient`. Create one per
application and reuse it; creating one per request exhausts sockets. Dispose it only when it owns
its transport — the constructors above do; the `HttpClient` overload does not, leaving that client's
lifetime to you.

**Transport hardening.** `BaseUrl` must be an absolute HTTPS URL whose path is exactly `/v1`; user
info, extra path segments, query strings, and fragments are rejected. An SDK-owned transport
disables automatic redirects, because .NET forwards custom headers such as `X-Api-Key` to a
redirect target. If you supply your own `HttpClient`, its `BaseAddress` must match `BaseUrl` and
**you** must disable redirects on its primary handler:

```csharp
using var http = new HttpClient(AssinafyClient.CreatePrimaryHandler())
{
    BaseAddress = new Uri("https://sandbox.assinafy.com.br/v1/"),   // note the trailing slash
};
using var client = new AssinafyClient(
    new AssinafyClientOptions
    {
        ApiKey = apiKey,
        AccountId = accountId,
        BaseUrl = "https://sandbox.assinafy.com.br/v1",
    },
    http);
```

`AssinafyClient.CreatePrimaryHandler()` returns exactly the handler the SDK uses for its own
transport — redirects disabled, five-minute pooled connection lifetime, and TLS 1.2 or 1.3 only,
because Assinafy refuses older protocols during the handshake (a `NetworkException`, never an HTTP
status).

Credentials are attached per request, so a supplied `HttpClient`'s default headers are never
mutated and the instance stays safe to share. Its `Timeout` is left untouched — set it yourself.

## OAuth 2.1 for multi-workspace apps

Use OAuth when **other people** connect your application to **their own** Assinafy workspace, so you
never handle their password or API key. Automating your own workspace needs none of this — keep
using an API key.

| | API key | OAuth |
|---|---|---|
| Acts on | Your own workspace | Someone else's, with their permission |
| Can do | Everything your account can | Only the scopes the user approved |
| User can switch it off | No | Yes, at any time |

The flow spans two hosts on purpose: the approval page belongs to the authorization server
(`https://auth.assinafy.com.br`), while every call your code makes — the token exchange included —
belongs to this API. Register the application under **Settings → OAuth applications** in the
Assinafy app; redirect URIs must be `https://` and are matched character for character, so
`…/callback` and `…/callback/` are different URIs.

### 1. Start the connection

PKCE is mandatory for every application, confidential ones included. Create a new pair per attempt
and keep both values in the user's session:

```csharp
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;

var pkce = OAuthResource.CreatePkcePair();     // 256-bit verifier + its S256 challenge
var state = OAuthResource.CreateState();       // CSRF protection

HttpContext.Session.SetString("assinafy_verifier", pkce.CodeVerifier);
HttpContext.Session.SetString("assinafy_state", state);

var authorizationUrl = OAuthResource.BuildAuthorizationUrl(new OAuthAuthorizationRequest
{
    ClientId = clientId,
    RedirectUri = "https://myapp.example.com/oauth/callback",
    Scopes =
    [
        OAuthScopes.DocumentsRead,
        OAuthScopes.DocumentsWrite,
        OAuthScopes.OfflineAccess,      // ask for this to receive a refresh token
    ],
    State = state,
    CodeChallenge = pkce.CodeChallenge,
});

return Redirect(authorizationUrl.ToString());   // a full page navigation, never an AJAX call
```

### 2. Handle the redirect and exchange the code

The user comes back with `?code=…&state=…&iss=…`, or `?error=access_denied&…` if they declined.
Validate `state` and `iss` **before anything else** — if either differs, the response is not yours.
The code is single-use and expires 60 seconds after approval, so exchange it from your server
straight away:

```csharp
if (state is null ||
    state != HttpContext.Session.GetString("assinafy_state") ||
    iss != OAuthResource.DefaultIssuer)
    return BadRequest();

if (error is not null)          // access_denied, invalid_scope, invalid_request, …
    return View("ConnectionFailed", error);

var tokens = await oauth.OAuth.ExchangeCodeAsync(new OAuthCodeExchangeRequest
{
    Code = code,
    RedirectUri = "https://myapp.example.com/oauth/callback",
    CodeVerifier = HttpContext.Session.GetString("assinafy_verifier")!,
    ClientId = clientId,
    ClientSecret = clientSecret,    // omit entirely for a public application
});
```

`oauth` is a client your application creates once, as
`new AssinafyClient(new AssinafyClientOptions())`, and uses only for token and revoke calls. It
needs no credential, since those endpoints authenticate with the application's own, and it owns its
transport, so no retry or hedging handler can resend a one-time code or a refresh token (see
[Dependency injection](#dependency-injection)). Read `tokens.Scope` rather than assuming the
request was granted in full:

```csharp
if (!tokens.HasScope(OAuthScopes.DocumentsWrite))
    return View("ReconnectWithWriteAccess");
```

### 3. Find the workspace, then call the API

A token belongs to the **one** workspace the user picked. With an OAuth token the workspace list
returns exactly that workspace — store its ID beside the tokens:

```csharp
using var client = new AssinafyClient(new AssinafyClientOptions { Token = tokens.AccessToken });

var accounts = await client.Accounts.ListAsync();
var accountId = accounts[0].Id;                 // the workspace the user connected

var documents = await client.Documents.ListAsync(accountId: accountId);
```

Calling any other workspace returns `403`, even another one the same user belongs to. If a customer
uses several workspaces, connect each separately and keep tokens per workspace.

### 4. Refresh, and handle a missing scope

Access tokens last one hour. With `offline_access` you can renew without the user: when a call
answers `401`, refresh once, then repeat the call with a client built from the new access token,
because a client keeps the token it was constructed with:

```csharp
var sent = await LoadRefreshTokenAsync(connectionId);   // under this connection's refresh lock

var refreshed = await oauth.OAuth.RefreshTokenAsync(new OAuthRefreshRequest
{
    RefreshToken = sent,
    ClientId = clientId,
    ClientSecret = clientSecret,
});

await SaveTokensAsync(connectionId, refreshed);   // do this FIRST: refresh tokens rotate

using var client = new AssinafyClient(new AssinafyClientOptions { Token = refreshed.AccessToken });
```

> **Using a retired refresh token disconnects the user.** Every refresh returns a new refresh token
> and invalidates the old one; `RefreshTokenAsync` throws a `SerializationException` when a response
> carries no new one. A replayed token cannot be told apart from a stolen one, so it ends the whole
> connection. Persist the new token before doing anything else, refresh one at a time per
> connection, and never resend a refresh token automatically, from your own code or through a
> retry handler. A refresh token is valid for 30 days and every refresh returns a new one with a
> fresh 30 days, so a connection expires only if your app goes 30 days without refreshing; after
> that, the user must connect again.

**If a refresh fails**, the server may still have rotated the token: a timeout or a lost response
looks the same as a request that never arrived. Re-read your stored token and continue only if
another worker has saved a *different* one. If it is still the token you sent, never send it again:
treat the connection as uncertain and ask the user to connect again. Only failures that provably
happened before the request was sent are safe to retry — DNS resolution, a refused connection, or
the TLS handshake, which surface as a `NetworkException` whose inner `HttpRequestException` has an
`HttpRequestError` of `NameResolutionError`, `ConnectionError`, or `SecureConnectionError`.

Calling an endpoint the token was never granted returns `403` with a challenge naming the scope.
The SDK surfaces it on every endpoint, not only the OAuth ones:

```csharp
try
{
    await client.Documents.UploadAsync(pdf, "contract.pdf", accountId);
}
catch (OAuthException ex) when (ex.Error == "insufficient_scope")
{
    // Reconnect requesting ex.Scope — do not retry, the answer will not change.
    return Redirect(BuildReconnectUrl(ex.Scope));
}
catch (OAuthException ex) when (ex.Error == "invalid_grant")
{
    // The grant is spent or the user reconnected with different permissions.
    return Redirect(BuildReconnectUrl(null));
}
```

### 5. Disconnect

When a user disconnects in your product, revoke the token instead of only deleting your copy.
Revoking a refresh token ends the whole connection, and the endpoint answers `200` whatever the
token's state, one a refresh has already retired included, so a stale copy gives no sign of whether
the connection ended. Read the token from storage immediately before the call, under the same
per-connection lock you refresh with:

```csharp
var current = await LoadRefreshTokenAsync(connectionId);   // under this connection's refresh lock

await oauth.OAuth.RevokeAsync(new OAuthRevokeRequest
{
    Token = current,
    ClientId = clientId,
    ClientSecret = clientSecret,
    TokenTypeHint = "refresh_token",
});
```

### Scopes

| `OAuthScopes` constant | Value | Lets your app |
|---|---|---|
| `DocumentsRead` | `documents:read` | Read documents, their signers, assignments, and activity |
| `DocumentsWrite` | `documents:write` | Create documents and send them for signature |
| `TemplatesRead` | `templates:read` | Read templates |
| `TemplatesWrite` | `templates:write` | Create and change templates |
| `AccountRead` | `account:read` | Read the workspace profile, theme, and logo |
| `WebhooksWrite` | `webhooks:write` | Configure and deactivate the workspace webhook subscription |
| `OpenId` | `openid` | Receive an `id_token` identifying the user |
| `Profile` | `profile` | Read the user's name |
| `Email` | `email` | Read the user's email and whether it is verified |
| `OfflineAccess` | `offline_access` | Receive a refresh token |

Request the minimum: the user approves everything or nothing, and `documents:write` can spend the
workspace's notification credits. Billing, workspace membership, credentials, and administration are
never reachable with an OAuth token, whatever its scopes.

### OpenID Connect and discovery

Request `openid` (plus `profile` and/or `email`) to receive a signed `id_token`, and read the
claims back from the userinfo endpoint:

```csharp
var who = await client.OAuth.GetUserInfoAsync();
Console.WriteLine($"{who.Sub} · {who.Name} · {who.Email} (verified: {who.EmailVerified})");
```

Validate an `id_token` with any OpenID Connect library: `RS256`, keys at
`https://auth.assinafy.com.br/.well-known/jwks.json` matched by `kid`, `iss` equal to
`OAuthResource.DefaultIssuer`, `aud` equal to your `client_id`, `exp` in the future, and `nonce`
equal to the one you sent, if any.

Rather than hard-coding endpoints, discover them:

```csharp
var metadata = await client.OAuth.GetProtectedResourceMetadataAsync();
// metadata.AuthorizationServers[0] → fetch its /.well-known/oauth-authorization-server
```

### Token-endpoint errors

| `Error` | Usual cause | What to do |
|---|---|---|
| `invalid_grant` | Code expired or already used; wrong `code_verifier` or `redirect_uri`; refresh token spent, or the user reconnected with different permissions | Send the user through the flow again |
| `invalid_client` | Wrong `client_id` or secret, or the application is disabled | Fix the configuration |
| `invalid_target` | `resource` disagrees with the authorized value | Send the same `Resource` to both endpoints |
| `unsupported_grant_type` | Only `authorization_code` and `refresh_token` exist | Fix the call |
| `insufficient_scope` | A `403` on an ordinary endpoint; the token lacks the permission in `Scope` | Reconnect requesting that scope |

New applications are unverified: the approval screen says so and they connect to at most 25
workspaces. The authorize and token endpoints accept 50 requests per minute per IP.

## Dependency injection

The package has **no NuGet dependencies** and ships no container adapter, so it never drags a
`Microsoft.Extensions.*` version into your application. Register it with the DI stack you already
have — the `HttpClient` constructor is the extension point:

```csharp
builder.Services
    .AddHttpClient("Assinafy", http =>
    {
        http.BaseAddress = new Uri("https://api.assinafy.com.br/v1/");   // note the trailing slash
        http.Timeout = TimeSpan.FromSeconds(30);
    })
    .ConfigurePrimaryHttpMessageHandler(AssinafyClient.CreatePrimaryHandler)
    .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

builder.Services.AddSingleton(serviceProvider => new AssinafyClient(
    new AssinafyClientOptions
    {
        ApiKey = builder.Configuration["Assinafy:ApiKey"],
        AccountId = builder.Configuration["Assinafy:AccountId"],
        BaseUrl = "https://api.assinafy.com.br/v1",
    },
    serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("Assinafy")));
```

Then inject `AssinafyClient` anywhere. Four details matter:

- **`ConfigurePrimaryHttpMessageHandler(AssinafyClient.CreatePrimaryHandler)`** disables automatic
  redirects, so an API key is never forwarded to a redirect target. Do not skip it.
- **`SetHandlerLifetime(Timeout.InfiniteTimeSpan)`** — the singleton captures one `HttpClient`, so it
  cannot observe factory handler rotation. `CreatePrimaryHandler` recycles connections through
  `PooledConnectionLifetime` instead, which is what keeps DNS changes visible.
- **`BaseAddress` must match `BaseUrl`**, with a trailing slash on the `Uri`.
- **Set `Timeout` on the `HttpClient`.** The `AssinafyClientOptions.Timeout` value is ignored for a
  supplied client, because the SDK does not mutate a transport it does not own.

Resilience policies and extra handlers chain onto the `IHttpClientBuilder` as usual. Limit retries
to safe methods: the standard handler retries every method by default, and repeating a `POST` can,
for example, upload the same document twice:

```csharp
using Microsoft.Extensions.Http.Resilience;   // 9.8+: older versions still retry timeouts

builder.Services
    .AddHttpClient("Assinafy", /* … */)
    .ConfigurePrimaryHttpMessageHandler(AssinafyClient.CreatePrimaryHandler)
    .AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());
```

Do not dispose the resolved client yourself; `IHttpClientFactory` owns the transport.

**Keep OAuth token and revoke calls off the factory.** Resending `POST /oauth/token` replays a
one-time code or a refresh token that may already have rotated, which disconnects the user, and
configuring the handlers you add cannot rule that out: a retry or hedging handler registered with
`ConfigureHttpClientDefaults` applies to every client the factory creates, a separate named client
included; a hedging handler (`AddStandardHedgingHandler`) sends parallel copies by design; and
`Microsoft.Extensions.Http.Resilience` before 9.8 retried timeouts even with
`DisableForUnsafeHttpMethods`. A client constructed without an `HttpClient` owns a transport the
factory never touches — `CreatePrimaryHandler()` and no other handler — so register one for
`ExchangeCodeAsync`, `RefreshTokenAsync`, and `RevokeAsync`:

```csharp
builder.Services.AddKeyedSingleton("AssinafyOAuth", (_, _) =>
    new AssinafyClient(new AssinafyClientOptions()));   // no credential of its own
```

Inject it as `[FromKeyedServices("AssinafyOAuth")] AssinafyClient oauth`.

## How requests and responses work

**The envelope.** Every Assinafy response is `{ status, message, data }`. The SDK unwraps it: your
method returns the `data` payload, already typed. A `status` of 400 or above becomes an
`ApiException` regardless of the HTTP status line, so a "200 OK" carrying an error envelope still
throws.

**Naming.** Request and response bodies use `snake_case`, handled for you. The single exception is
the signer sign body, which the API defines in camelCase; `SignAssignmentValue` applies that
automatically.

**Lists.** Two shapes exist, and the return type tells you which:

- `IReadOnlyList<T>` — the endpoint returns a complete array (tags, activities, statistics, event
  types, field types, statuses).
- `PaginatedResult<T>` — the endpoint pages. `Data` holds the page; `Meta` carries `CurrentPage`,
  `PerPage`, `Total`, and `LastPage`, parsed from the `X-Pagination-*` response headers. `Meta` is
  `null` when the response carried no such headers.

Paging parameters are `page` (1-based) and `per-page` (max 100), alongside `search` and `sort`
where the endpoint supports them:

```csharp
var page = await client.Documents.ListAsync(new Dictionary<string, string?>
{
    ["status"] = "pending_signature",
    ["sort"] = "-created_at",
    ["page"] = "1",
    ["per-page"] = "50",
});

Console.WriteLine($"{page.Data.Count} of {page.Meta?.Total} documents");

while (page.Meta is { CurrentPage: int current, LastPage: int last } && current < last)
{
    page = await client.Documents.ListAsync(new Dictionary<string, string?>
    {
        ["page"] = (current + 1).ToString(),
        ["per-page"] = "50",
    });
    // …process page.Data
}
```

**Cancellation and timeouts.** Every method takes a trailing `CancellationToken`. A client-side
timeout surfaces as `NetworkException`; a token you cancelled yourself propagates as
`OperationCanceledException`, unchanged.

**Rate limiting.** The API returns `429` when a caller exceeds its quota. The SDK does not retry
automatically — surface it, back off, and retry, or chain a resilience handler onto the
`IHttpClientBuilder` shown in [Dependency injection](#dependency-injection).
`Documents.WaitUntilReadyAsync` is the one exception: it treats `404`, `429`, and `5xx` as transient
while polling.

## Error handling

Every SDK-specific exception derives from `AssinafyException`:

| Exception | Raised when | Key members |
|---|---|---|
| `ValidationException` | The SDK rejects input before any HTTP call | `Details` (field-level) |
| `ApiException` | The API returned an error status or envelope | `StatusCode`, `ApiMessage`, `Details` |
| `OAuthException` | The failure carries a machine-readable OAuth error code, from an OAuth endpoint or an `insufficient_scope` challenge on any endpoint. Derives from `ApiException` | `Error`, `ErrorDescription`, `Scope` |
| `NetworkException` | Connection, DNS, or TLS failure, or a client-side timeout | `InnerException` |
| `SerializationException` | A body could not be serialized, or a success response did not match the expected envelope or payload | `InnerException` |

Standard .NET argument, cancellation, disposal, and stream exceptions keep their platform types.

```csharp
try
{
    await client.Documents.GetAsync(documentId);
}
catch (ApiException ex) when (ex.StatusCode == 404)
{
    Console.WriteLine($"Not found: {ex.ApiMessage}");
}
catch (ApiException ex) when (ex.StatusCode == 429)
{
    // Back off and retry.
}
catch (ApiException ex)
{
    Console.WriteLine($"{ex.StatusCode}: {ex.ApiMessage}");
    Console.WriteLine(ex.Details?.GetRawText());   // structured field errors, when supplied
}
catch (NetworkException ex)
{
    Console.WriteLine($"Transport failure: {ex.Message}");
}
```

## The signature lifecycle

A signature request moves through five stages. Everything else in this SDK supports one of them.

1. **Upload** a PDF into a workspace, producing a document in `uploaded` status.
2. **Wait** for the platform to normalize it and extract pages (`metadata_ready`).
3. **Create signers** — reusable people records belonging to the workspace.
4. **Create an assignment**, binding signers to the document. This is what sends the invitations.
5. **Signers sign**, and once the last one finishes the document becomes `certificated` and its
   signed artifacts become downloadable.

End to end:

```csharp
using Assinafy.Sdk;
using Assinafy.Sdk.Models;

using var client = new AssinafyClient(new AssinafyClientOptions
{
    ApiKey = Environment.GetEnvironmentVariable("ASSINAFY_API_KEY"),
    AccountId = Environment.GetEnvironmentVariable("ASSINAFY_ACCOUNT_ID"),
});

// 1–2. Upload and wait for the document to be ready.
await using var pdf = File.OpenRead("contract.pdf");
var document = await client.Documents.UploadAsync(pdf, "contract.pdf");
await client.Documents.WaitUntilReadyAsync(document.Id);

// 3. Create the signer.
var signer = await client.Signers.CreateAsync(new CreateSignerRequest
{
    FullName = "John Doe",
    Email = "john@example.com",
});

// 4. Request the signature. This sends the invitation.
var assignment = await client.Assignments.CreateAsync(document.Id, new CreateAssignmentRequest
{
    Method = AssignmentMethods.Virtual,
    Message = "Please review and sign.",
    Signers =
    [
        new SignerRef
        {
            Id = signer.Id,
            VerificationMethod = SignerChannels.Email,
            NotificationMethods = [SignerChannels.Email],
        },
    ],
});

// assignment.SigningUrls carries a per-signer link if you would rather deliver it yourself.

// 5. Wait for signing to finish, then download the certified PDF.
using var deadline = new CancellationTokenSource(TimeSpan.FromHours(1));
DocumentDetails completed;
do
{
    await Task.Delay(TimeSpan.FromSeconds(10), deadline.Token);
    completed = await client.Documents.GetAsync(document.Id, deadline.Token);
}
while (!string.Equals(completed.Status, "certificated", StringComparison.OrdinalIgnoreCase));

var certified = await client.Documents.DownloadAsync(document.Id);
await File.WriteAllBytesAsync("contract-signed.pdf", certified);
```

Polling is shown for clarity. In production, subscribe to the `document_ready` [webhook](#webhooks)
instead of polling.

Steps 1 through 4 collapse into a single call when you do not need to inspect the intermediate
results:

```csharp
await using var pdf = File.OpenRead("contract.pdf");
var result = await client.UploadAndRequestSignaturesAsync(new UploadAndRequestSignaturesOptions
{
    FileStream = pdf,
    FileName = "contract.pdf",
    Message = "Please review and sign.",
    Signers =
    [
        new UploadAndRequestSignaturesSigner
        {
            FullName = "John Doe",
            Email = "john@example.com",
            VerificationMethod = SignerChannels.Email,
            NotificationMethods = [SignerChannels.Email],
        },
    ],
});

// result.Document, result.Assignment, result.SignerIds
```

The helper is deliberately **not** transactional, because the API has no transaction spanning
upload, signer creation, and assignment creation. If a later request fails, the resources already
created remain for you to inspect or clean up.

### Two assignment methods

| Method | What it does | Requires |
|---|---|---|
| `AssignmentMethods.Virtual` | Signers are notified and sign remotely, at their convenience | Document in `uploaded`, `metadata_processing`, or `metadata_ready` |
| `AssignmentMethods.Collect` | Field values are collected in-session against explicit page and field placements | Document in `metadata_ready`, plus `Entries` |

A collect assignment needs an entry per page describing which signer fills which field:

```csharp
var collect = await client.Assignments.CreateAsync(document.Id, new CreateAssignmentRequest
{
    Method = AssignmentMethods.Collect,
    Signers = [new SignerRef { Id = signer.Id }],
    Entries =
    [
        new AssignmentEntry
        {
            PageId = document.Pages[0].Id,
            Fields =
            [
                new AssignmentEntryField
                {
                    SignerId = signer.Id,
                    FieldId = fieldId,
                    DisplaySettings = new DisplaySettings
                    {
                        Left = 100, Top = 640, Width = 220, Height = 40, FontSize = 12,
                    },
                },
            ],
        },
    ],
});
```

When the signers do not exist yet, `UploadAndRequestSignaturesAsync` exposes `EntriesFactory`,
which runs after signer creation and receives the new IDs:

```csharp
EntriesFactory = signerIds =>
[
    new AssignmentEntry
    {
        PageId = pageId,
        Fields = [new AssignmentEntryField { SignerId = signerIds[0], FieldId = fieldId }],
    },
],
```

### Previewing cost

Assignments consume plan documents and notification credits. Every committing call has a matching
estimate that charges nothing:

```csharp
var estimate = await client.Assignments.EstimateCostAsync(document.Id, request);

if (!estimate.HasSufficientResources)
    throw new InvalidOperationException(estimate.BlockingReason);

Console.WriteLine($"{estimate.TotalCredits} credits, {estimate.CreditBalance} available");
```

`Documents.EstimateCostFromTemplateAsync` and `Assignments.EstimateResendCostAsync` do the same for
the template and resend flows.

## Documents

```csharp
// Upload — PDF only, 25MB maximum.
await using var pdf = File.OpenRead("contract.pdf");
var document = await client.Documents.UploadAsync(pdf, "contract.pdf");

// Read
var details   = await client.Documents.GetAsync(document.Id);
var listing   = await client.Documents.ListAsync();
var matches   = await client.Documents.SearchAsync("contract", perPage: 20);
var statuses  = await client.Documents.ListStatusesAsync();
var timeline  = await client.Documents.ActivitiesAsync(document.Id);

// Rename — only before an assignment exists; the server normalizes the result.
var renamed = await client.Documents.RenameAsync(document.Id, "Contract 2026");

// Binary artifacts
var original  = await client.Documents.DownloadAsync(document.Id, DocumentArtifactNames.Original);
var pades     = await client.Documents.DownloadAsync(document.Id, DocumentArtifactNames.Pades);
var thumbnail = await client.Documents.ThumbnailAsync(document.Id);
var pageImage = await client.Documents.DownloadPageAsync(document.Id, details.Pages[0].Id);

// Public verification of a signed document, by its signature hash — no credentials needed.
var verification = await client.Documents.VerifyAsync(signatureHash);

await client.Documents.DeleteAsync(document.Id);
```

`ListAsync` accepts `status`, `method`, `search`, `tags` (comma-separated tag IDs, matching
documents that carry all of them), `sort`, `page`, and `per-page`. `SearchAsync` hits the compact
search route, which omits the expanded `assignment` and `pages` that `ListAsync` returns.

Three local helpers save round trips of your own:

```csharp
var ready    = await client.Documents.WaitUntilReadyAsync(document.Id, maxWait: TimeSpan.FromMinutes(2));
var signed   = await client.Documents.IsFullySignedAsync(document.Id);
var progress = await client.Documents.GetSigningProgressAsync(document.Id);

Console.WriteLine($"{progress.Signed}/{progress.Total} ({progress.Percentage}%)");
```

## Templates

A template is a reusable PDF with named roles and field placements. Instantiating one produces an
ordinary document.

```csharp
await using var file = File.OpenRead("nda.pdf");
var template = await client.Templates.CreateAsync(file, "nda.pdf", name: "Mutual NDA");

// Pages and roles appear once processing finishes.
var ready = await client.Templates.GetAsync(template.Id);
var pageImage = await client.Templates.DownloadPageAsync(ready.Id, ready.Pages[0].Id);

var templates = await client.Templates.ListAsync(new Dictionary<string, string?>
{
    ["search"] = "nda",
});

await client.Templates.UpdateAsync(template.Id, new UpdateTemplateRequest
{
    Name = "Mutual NDA v2",
    Message = "Please review and sign.",
});

// Instantiate: bind existing signers to the template's roles.
var fromTemplate = await client.Documents.CreateFromTemplateAsync(
    template.Id,
    [new TemplateSigner { RoleId = ready.Roles[0].Id, Id = signer.Id }],
    new CreateDocumentFromTemplateOptions
    {
        Name = "NDA — Acme",
        ExpiresAt = "2026-12-31T23:59:59Z",
        EditorFields = [new TemplateEditorField { FieldId = fieldId, Value = "Acme Inc." }],
    });

await client.Templates.DeleteAsync(template.Id);
```

The template display name is carried as the multipart file name, so `CreateAsync` appends `.pdf` to
`name` when needed rather than sending a second form field.

## Signers

A signer is a workspace-level person record, reused across documents.

```csharp
var signer = await client.Signers.CreateAsync(new CreateSignerRequest
{
    FullName = "John Doe",
    Email = "john@example.com",
    WhatsAppPhoneNumber = "+5511999999999",   // E.164; normalized on save
});

var page   = await client.Signers.ListAsync(new Dictionary<string, string?> { ["search"] = "john" });
var one    = await client.Signers.GetAsync(signer.Id);
var byMail = await client.Signers.FindByEmailAsync("john@example.com");   // exact match, or null

await client.Signers.UpdateAsync(signer.Id, new UpdateSignerRequest { GovernmentId = "00000000000" });
await client.Signers.DeleteAsync(signer.Id);
```

`FindByEmailAsync` follows every result page and returns the first exact, case-insensitive match —
the server-side `search` is a fuzzy filter, so it alone is not enough. Email and WhatsApp number
cannot be changed while the signer has a verified channel on a document that is still in flight.

## Assignments

```csharp
var assignments = await client.Assignments.ListAsync(new AssignmentListParams { PerPage = 50 });

// Set or clear the expiration.
await client.Assignments.ResetExpirationAsync(documentId, assignmentId, "2026-12-31T23:59:59Z");
await client.Assignments.ResetExpirationAsync(documentId, assignmentId, null);

// Re-notify a single signer (estimate first — this can cost credits).
var resendCost = await client.Assignments.EstimateResendCostAsync(documentId, assignmentId, signerId);
var resend = await client.Assignments.ResendNotificationAsync(documentId, assignmentId, signerId);

// WhatsApp messages rendered for this assignment.
var messages = await client.Assignments.ListWhatsAppNotificationsAsync(documentId, assignmentId);
```

Per-signer options on `SignerRef` control how each person is verified and notified:

```csharp
new SignerRef
{
    Id = signer.Id,
    VerificationMethod = SignerChannels.Whatsapp,             // how identity is proven
    NotificationMethods = [SignerChannels.Email, SignerChannels.Whatsapp],
    Step = 1,                                                 // signing order; same step signs in parallel
}
```

`Step` drives sequential signing: signers sharing a step are notified together, and the next step is
notified only once the previous one completes. `SignerChannels.Whatsapp` is paid-only and costs
extra credits. `SignerChannels.DigitalCertificate` is a verification method only.

## The signer-facing flow

These endpoints belong to the person signing, not to your workspace. They authenticate with the
per-assignment **signer access code** carried in the signing link, and the SDK deliberately does
**not** attach your API key or bearer token to any of them.

Implement them when you host the signing experience yourself; skip the whole section if you let
Assinafy notify signers and host the signing page.

```csharp
// Load everything the signer needs.
var toSign = await client.Signing.GetAsync(signerAccessCode);
var profile = await client.Signers.GetSelfAsync(signerAccessCode);

// Record acceptance of the terms.
await client.Signers.AcceptTermsAsync(signerAccessCode);

// Verify a one-time code delivered by email or WhatsApp.
await client.Signers.VerifyAsync(signerAccessCode, verificationCode);

// Virtual assignments require confirmed signer data before signing; otherwise the API returns 400.
await client.Signers.ConfirmDataAsync(documentId, signerAccessCode, new ConfirmSignerDataRequest
{
    FullName = "John Doe",
    Email = "john@example.com",
    GovernmentId = "00000000000",
});

// Submit the field values.
await client.Signing.SignAsync(documentId, assignmentId, signerAccessCode,
[
    new SignAssignmentValue
    {
        ItemId = item.Id,
        FieldId = item.FieldId,
        PageId = item.PageId,
        Value = "John Doe",
    },
]);

// Or decline, with a reason.
await client.Signing.DeclineAsync(documentId, assignmentId, signerAccessCode, "Wrong counterparty");
```

A signer with several pending documents can act on them in bulk, browse their own documents, and
download finished artifacts:

```csharp
await client.Signing.SignMultipleAsync(signerAccessCode, [documentId1, documentId2]);
await client.Signing.DeclineMultipleAsync(signerAccessCode, [documentId3], "Not applicable");

var current = await client.Signing.GetCurrentDocumentAsync(signerId, signerAccessCode);
var mine    = await client.Signing.ListDocumentsAsync(signerId, signerAccessCode,
                  new SignerDocumentListParams { PerPage = 25 });
var found   = await client.Signing.SearchDocumentsAsync(signerId, signerAccessCode,
                  new SignerDocumentListParams { Search = "nda" });

var artifact = await client.Signing.DownloadPublicAsync(
    signerId, documentId, DocumentArtifactNames.Certificated);
```

### Digital-certificate signing

When an assignment's verification method is `DigitalCertificate`, signing is a two-step Web PKI
exchange instead of a field submission:

```csharp
var operation = await client.Signing.StartCertificateAsync(signerAccessCode);
var signedToken = await SignWithWebPkiAsync(operation.Token);   // your browser/Web PKI bridge
var result = await client.Signing.CompleteCertificateAsync(signerAccessCode, signedToken);

Console.WriteLine(result.SignerName);   // read from the certificate
```

Both routes are production-only deployed extensions: the sandbox does not expose them and they are
absent from the published OpenAPI document. They require a real production certificate assignment
and a browser-signed Web PKI token.

## Signature images

A signer's drawn signature and initials, stored for reuse across documents.

```csharp
await using var png = File.OpenRead("signature.png");
await client.Signatures.UploadAsync(
    png,
    signerAccessCode,
    reuse: true,                              // allow reuse in future signing processes
    type: SignatureImageTypes.Signature);     // or SignatureImageTypes.Initial

var image = await client.Signatures.DownloadAsync(signerAccessCode, SignatureImageTypes.Signature);
```

`Signer.HasSignature`, `HasInitial`, and `IsSignatureReusable` (returned by `GetSelfAsync`) tell you
whether a stored image exists and whether the signer agreed to reuse it. When
`IsSignatureReusable` is `false`, do not pre-render the stored image even if one exists.

## Public documents

Unauthenticated lookups for a recipient who has a document link.

```csharp
var info = await client.PublicDocuments.GetDetailsAsync(documentId);
await client.PublicDocuments.SendTokenAsync(documentId, "john@example.com");
```

`SendTokenAsync` asks the API to email a signing access token; omit the address to use the
document's configured recipient.

## Tags

Tags are unique per workspace, case-insensitively, and attach to documents.

```csharp
var tag = await client.Tags.CreateAsync(new CreateTagRequest { Name = "Contracts", Color = "3366FF" });
var tags = await client.Tags.ListAsync(search: "con");

await client.Tags.UpdateAsync(tag.Id, new UpdateTagRequest { Color = "FF6600" });
await client.Tags.UpdateAsync(tag.Id, new UpdateTagRequest { ClearColor = true });

// Attach keeps existing tags; set replaces the whole set (pass [] to clear).
var attached = await client.Tags.AddToDocumentAsync(documentId, [tag.Id]);
var replaced = await client.Tags.SetForDocumentAsync(documentId, [tag.Id]);
var onDoc    = await client.Tags.ListForDocumentAsync(documentId);

await client.Tags.RemoveFromDocumentAsync(documentId, tag.Id);
await client.Tags.DeleteAsync(tag.Id, force: true);   // force detaches everywhere first
```

Creating a duplicate name returns `409`. Deleting a tag that is still attached returns `409` unless
`force` is `true`. `DeleteWithResultAsync` and `RemoveFromDocumentWithResultAsync` are variants that
return the API's `{ deleted }` / `{ detached }` payload instead of nothing.

## Fields

Field definitions describe the typed inputs a signer fills in — with an optional regular expression
the platform validates against.

```csharp
var types = await client.Fields.ListTypesAsync();

var field = await client.Fields.CreateAsync(new CreateFieldDefinitionRequest
{
    Name = "Customer reference",
    Type = "text",
    Regex = "^[A-Z]{3}-[0-9]{4}$",
    IsRequired = true,
});

var fields = await client.Fields.ListAsync(new FieldListParams { IncludeStandard = true });

await client.Fields.UpdateAsync(field.Id, new UpdateFieldDefinitionRequest { IsActive = false });
await client.Fields.UpdateAsync(field.Id, new UpdateFieldDefinitionRequest { ClearRegex = true });

// Validate before submitting, either as the account or on a signer's behalf.
var check = await client.Fields.ValidateAsync(
    field.Id,
    new ValidateFieldValueRequest { Value = "ACM-1234" },
    signerAccessCode);

var checks = await client.Fields.ValidateMultipleAsync(
[
    new ValidateFieldValueItem { FieldId = field.Id, Value = "ACM-1234" },
]);

await client.Fields.DeleteAsync(field.Id);
```

Deleting a field already used on a document fails.

## Webhooks

A workspace has one subscription. Prefer it over polling for document state.

```csharp
var events = await client.Webhooks.ListEventTypesAsync();

await client.Webhooks.UpdateSubscriptionAsync(new UpdateWebhookSubscriptionRequest
{
    Url = "https://example.com/webhooks/assinafy",
    Email = "ops@example.com",
    IsActive = true,
    Events = ["document_ready", "signer_signed_document", "signer_rejected_document"],
});

var subscription = await client.Webhooks.GetAsync();

// Delivery history and replay.
var history = await client.Webhooks.ListDispatchesAsync(new ListDispatchesParams
{
    Delivered = false,
    From = DateTimeOffset.UtcNow.AddDays(-7).ToUnixTimeSeconds(),
    PerPage = 50,
});

foreach (var failed in history.Data)
    await client.Webhooks.RetryDispatchAsync(failed.Id);

// Pause delivery without losing the configuration.
await client.Webhooks.InactivateAsync();
```

There is no delete endpoint — `InactivateAsync`, or an update with `IsActive = false`, is how you
stop deliveries.

Deliveries arrive as the same `{ status, message, data }` envelope your endpoint should acknowledge
with a `2xx`. `assignment_created` and `document_metadata_ready` have no guaranteed ordering, and
unknown fields are forward-compatible additions — ignore rather than reject them. The full event
catalog, payload keys, and delivery contract are in
[docs/API.md](docs/API.md#webhook-payloads).

## Accounts and users

```csharp
var accounts = await client.Accounts.ListAsync();          // discover account IDs
var account  = await client.Accounts.GetAsync();
var theme    = await client.Accounts.GetThemeAsync();      // branding colors and logo URL

await client.Accounts.UpdateAsync(new UpdateAccountRequest
{
    Name = "Acme Legal",
    NotificationSenderType = AccountNotificationSenderTypes.Account,
});

await using var logo = File.OpenRead("logo.png");
await client.Accounts.UploadLogoAsync(logo, "logo.png");
var logoBytes = await client.Accounts.DownloadLogoAsync();
await client.Accounts.DeleteLogoAsync();

var user  = await client.Users.GetSelfAsync();
var prefs = await client.Users.GetNotificationPreferencesAsync();
await client.Users.UpdateNotificationPreferencesAsync(new UpdateNotificationPreferencesRequest
{
    DocumentCompleted = true,
    DocumentExpired = false,
});
```

`NotificationSenderType` decides whether signers see the individual user or the workspace as the
sender. Statistics are available per account and summed across every account the user belongs to:

```csharp
var monthly = await client.Accounts.GetStatsAsync(new DocumentStatsParams
{
    Granularity = DocumentStatsGranularities.Monthly,
});

var daily = await client.Users.GetStatsAsync(new DocumentStatsParams
{
    Granularity = DocumentStatsGranularities.Daily,
    Month = "2026-08",     // required for daily
});

foreach (var row in monthly)
    Console.WriteLine($"{row.Period}: {row.DocumentsSent} sent, {row.DocumentsCertified} certified");
```

API keys are managed through the `Authentication` resource. Generating a new key replaces the
previous one, and the full value is shown only once:

```csharp
var created = await client.Authentication.CreateApiKeyAsync(new CreateApiKeyRequest { Password = password });
var masked  = await client.Authentication.GetApiKeyAsync();
await client.Authentication.DeleteApiKeyAsync();
```

## Reference tables

**Document artifacts** (`DocumentArtifactNames`)

| Value | Contents |
|---|---|
| `original` | The uploaded PDF, unchanged |
| `certificated` | The signed and certificated PDF (default) |
| `certificate-page` | The standalone certificate page |
| `pades` | The signed PDF in PAdES format |
| `bundle` | The signed PDF bundled with the certificate page |

**Channels** (`SignerChannels`) — `Email` (free), `Whatsapp` (0.45 credits per signer, paid plans
only), `DigitalCertificate` (verification only; ICP-Brasil A1/A3 via Web PKI, 2 credits per signer
on top of its notification). Values are capitalized exactly as shown. Verification and notification
are coupled: `Email`↔`Email`, `Whatsapp`↔`Whatsapp`, and `DigitalCertificate` pairs with either.
Only the notification is billed.

**Assignment methods** (`AssignmentMethods`) — `virtual`, `collect`.

**Signature image types** (`SignatureImageTypes`) — `signature`, `initial`.

**Statistics granularity** (`DocumentStatsGranularities`) — `monthly`, `daily` (requires `Month`).

**Notification sender** (`AccountNotificationSenderTypes`) — `User`, `Account`.

**OAuth scopes** (`OAuthScopes`) — `documents:read`, `documents:write`, `templates:read`,
`templates:write`, `account:read`, `webhooks:write`, `openid`, `profile`, `email`, `offline_access`. See
[OAuth 2.1 for multi-workspace apps](#oauth-21-for-multi-workspace-apps).

Document status codes are not fixed constants; retrieve the live list, with each status's deletion
rule, from `Documents.ListStatusesAsync()`. The statuses `WaitUntilReadyAsync` treats as ready are
`metadata_ready`, `pending_signature`, and `certificated`.

## Testing

The regular suite runs on xUnit v3 over Microsoft.Testing.Platform against a stubbed HTTP transport,
on every supported target framework. Arguments after `--` are runner options:

```bash
dotnet test --solution Assinafy.Sdk.sln -- --filter-not-trait "Category=Live"
```

Without live-test credentials, a plain `dotnet test` skips the live tests. To run them, use a
sandbox account; they refuse any other base URL:

```bash
ASSINAFY_API_KEY=... \
ASSINAFY_ACCOUNT_ID=... \
ASSINAFY_BASE_URL=https://sandbox.assinafy.com.br/v1 \
dotnet test --project tests/Assinafy.Sdk.Tests/Assinafy.Sdk.Tests.csproj \
  --framework net10.0 -- --filter-trait "Category=Live"
```

`ASSINAFY_TEST_EMAIL_PRIMARY` and `ASSINAFY_TEST_EMAIL_SECONDARY` are optional overrides; without
them the suite uses reserved `example.com` addresses.

The sandbox suite does not exercise the production-only certificate routes. Local transport tests
cover their request construction, credential isolation, and response deserialization; the complete
flow needs a production certificate assignment and a browser-signed Web PKI token.

## Support matrix and versioning

| Target | .NET support | Ends |
|---|---|---|
| `net8.0` | LTS, maintenance | 2026-11-10 |
| `net9.0` | STS, maintenance | 2026-11-10 |
| `net10.0` | LTS, active | 2028-11-14 |

The package has no NuGet dependencies, so it never constrains which `Microsoft.Extensions.*`
version your application resolves.

Versioning is semantic. .NET package validation runs at pack time, so a binary-breaking change
cannot ship in a patch. Methods kept only for older call sites are marked `[Obsolete]` with the
replacement named in the message.

**Upgrading from 1.x:** `services.AddAssinafy(...)` was removed along with the SDK's
`Microsoft.Extensions.*` dependencies. Replace it with the registration in
[Dependency injection](#dependency-injection) — roughly ten lines in your composition root, using
packages your ASP.NET Core app already references. No other API changed.

## Further reading

- **[docs/API.md](docs/API.md)** — the complete reference: every public SDK method with its full
  signature, and all 89 production operations with request and response payloads, error bodies,
  authentication, and the webhook contract.
- **[docs/openapi.json](docs/openapi.json)** — the checked-in production OpenAPI snapshot. CI
  verifies it still matches the live document on every run.
- **[CHANGELOG.md](CHANGELOG.md)** — release history.
- **[SECURITY.md](SECURITY.md)** — vulnerability reporting.
- **[LICENSE](LICENSE)** — MIT.
