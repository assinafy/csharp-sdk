# Changelog

## 2.2.1

### Fixed

- Unconfigured live tests skip in a plain `dotnet test` run.

## 2.2.0

### Added

- **OAuth 2.1 support**, for applications that act inside another user's workspace with that user's
  permission. `client.OAuth` covers the whole flow:

  ```csharp
  var pkce = OAuthResource.CreatePkcePair();
  var state = OAuthResource.CreateState();          // store both in the user's session

  var url = OAuthResource.BuildAuthorizationUrl(new OAuthAuthorizationRequest
  {
      ClientId = clientId,
      RedirectUri = "https://myapp.example.com/oauth/callback",
      Scopes = [OAuthScopes.DocumentsRead, OAuthScopes.WebhooksWrite, OAuthScopes.OfflineAccess],
      State = state,
      CodeChallenge = pkce.CodeChallenge,
  });

  var tokens = await client.OAuth.ExchangeCodeAsync(new OAuthCodeExchangeRequest
  {
      Code = code,
      RedirectUri = "https://myapp.example.com/oauth/callback",
      CodeVerifier = pkce.CodeVerifier,
      ClientId = clientId,
      ClientSecret = clientSecret,
  });
  ```

  `ExchangeCodeAsync`, `RefreshTokenAsync`, `RevokeAsync`, `GetUserInfoAsync`, and
  `GetProtectedResourceMetadataAsync` cover the four OAuth endpoints; `CreatePkcePair`,
  `CreateState`, and `BuildAuthorizationUrl` cover the browser leg. PKCE (S256) is generated from a
  cryptographic RNG, and a `code_verifier` outside the RFC 7636 grammar is rejected before the
  one-time authorization code is spent on it.

- `OAuthScopes` constants for the ten permissions an application can request.
- `OAuthException`, carrying the machine-readable `Error`, `ErrorDescription`, and `Scope`. It
  derives from `ApiException`, so existing `catch (ApiException)` blocks keep working. A `403`
  answering with `WWW-Authenticate: Bearer error="insufficient_scope"` now surfaces the missing
  scope through `OAuthException.Scope` on **every** endpoint, not just the OAuth ones, so "reconnect
  asking for this permission" is distinguishable from any other `403`.
- `OAuthTokenResult.GrantedScopes` and `HasScope`, for reading what the user actually approved
  rather than assuming the request was granted in full.

### Changed

- `ApiException` is no longer `sealed`, so `OAuthException` can derive from it. No member changed.
- The OAuth endpoints are read and written as flat JSON, per RFC 6749 §5.1/§5.2, OpenID Connect
  §5.3.2, and RFC 9728, rather than through this API's `{ status, message, data }` envelope.
- `SignerChannels` documents the verification/notification coupling rules and the current prices:
  `Email` is free, `Whatsapp` costs 0.45 credits per signer, and `DigitalCertificate` adds 2 credits
  on top of its notification.
## 2.1.0

### Fixed

- Assignment cost estimates require at least one signer and include the `signers` field for both
  assignment methods.

## 2.0.0

### Removed (breaking)

- `AssinafyServiceCollectionExtensions.AddAssinafy` and the SDK's
  `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Http`
  dependencies. The package now has **no NuGet dependencies** and cannot constrain which
  `Microsoft.Extensions.*` version an application resolves.

  Register the client with the container packages your application already references:

  ```csharp
  builder.Services
      .AddHttpClient("Assinafy", http =>
      {
          http.BaseAddress = new Uri("https://api.assinafy.com.br/v1/");
          http.Timeout = TimeSpan.FromSeconds(30);
      })
      .ConfigurePrimaryHttpMessageHandler(AssinafyClient.CreatePrimaryHandler)
      .SetHandlerLifetime(Timeout.InfiniteTimeSpan);

  builder.Services.AddSingleton(serviceProvider => new AssinafyClient(
      options,
      serviceProvider.GetRequiredService<IHttpClientFactory>().CreateClient("Assinafy")));
  ```

  No other API changed.

### Added

- `AssinafyClient.CreatePrimaryHandler()` is now public. It returns the handler the SDK uses for
  its own transport — automatic redirects disabled, five-minute pooled connection lifetime — so a
  container-registered or caller-supplied `HttpClient` gets the same credential protection.

### Changed

- `Signing.DownloadAsync` is marked `[Obsolete]`, matching the other compatibility overloads. It
  ignores its `signerAccessCode` argument; use `Signing.DownloadPublicAsync`.
- The README is a complete integration guide: credentials and environments, client construction and
  lifetime, container registration, the response envelope, pagination, error handling, the signature
  lifecycle end to end, a section per resource, and reference tables.

## 1.3.2

### Added

- Template creation, update, deletion, and rendered-page downloads.
- Production ICP-Brasil certificate signing with `Signing.StartCertificateAsync` and
  `Signing.CompleteCertificateAsync`.
- `Signing.DownloadPublicAsync`, channel-neutral signer verification, and
  `EntriesFactory` support for collect assignments that use newly created signer IDs.

### Fixed

- Automatic redirects are disabled for SDK-owned transports, and base URLs are restricted to
  absolute HTTPS `/v1` endpoints so credentials cannot be forwarded to another host.
- Every user-controlled route segment is escaped and traversal values are rejected.
- Successful JSON responses now require a valid API envelope and the expected non-null payload.
- Document statistics preserve every current notification and verification counter while keeping
  the earlier email and WhatsApp property names as compatibility aliases.
- Upload limits use the stream's remaining bytes; unreadable or invalidly positioned streams and
  upload responses without IDs fail explicitly; signer email validation rejects whitespace-only values.
- Malformed credentials and request bodies that cannot be serialized are rejected before transport
  through the SDK exception hierarchy.
- `FindByEmailAsync` follows pagination even when response metadata is absent. `WaitUntilReadyAsync`
  retries transient `404`, `429`, and server errors and preserves the last failure on timeout.
- Webhook lookup propagates API errors.

### Build and documentation

- Builds and tests target `net8.0`, `net9.0`, and the current .NET 10 LTS SDK.
- Tests run on xUnit v3 with Microsoft.Testing.Platform and native Coverlet coverage collection.
- GitHub Actions use least-privilege permissions, immutable action revisions, environment-scoped
  secrets, short-lived NuGet credentials, provenance attestation, and package validation.
- The README, XML comments, SDK method reference, payload examples, and production OpenAPI
  snapshot cover the complete public surface.

## 1.3.1

- Added `net10.0`, the `Users` resource, account/user document statistics, notification
  preferences, typed field display settings, and stricter request/response models.
- Added package compatibility validation, deterministic documentation builds, the complete HTTP
  API reference, and the checked-in production OpenAPI snapshot.
- Expanded the sandbox lifecycle suite and the multi-target CI/package workflows.

## 1.3.0

### Added — endpoint coverage

- **New `Accounts` resource** covering all documented account endpoints:
  `ListAsync`, `GetAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync` (with `force`),
  `GetThemeAsync`, and logo `DownloadLogoAsync` / `UploadLogoAsync` / `DeleteLogoAsync`.
  Use `Accounts.ListAsync()` to discover the account ID that most other calls require.
- **`Assignments.ListAsync`** — `GET /assignments` (the account context is sent automatically
  via the `accountId` query parameter).
- **`Documents.RenameAsync`** — `PATCH /documents/{id}` (allowed before signing starts).
- **`Documents.SearchAsync`** — the compact `GET /accounts/{id}/documents/search` route.
- **`Signing.SearchDocumentsAsync`** — the compact `GET /signers/{id}/documents/search` route.
- **`Authentication.LinkSocialLoginAsync`** — `POST /auth/link-social-login` (bearer-token auth).

### Fixed — model fidelity (silent data loss)

- **`Signer.IsSignatureReusable`** added — `GET /signers/self` returns `is_signature_reusable`;
  when `false` a signer declined to reuse a saved signature, so clients must not pre-render it.
- **`Template.Tags` and `TemplateDetails.DefaultDocumentTags`** added — templates carry tag
  arrays that were previously discarded by the deserializer.
- **`AssignmentSigner.NotificationHistory`** is now a typed `IReadOnlyList<NotificationHistoryEntry>`
  (was an opaque `JsonElement`), so per-channel delivery history is directly usable.
- `DocumentPage`/`TemplatePage` `Height`/`Width` are now `int` (matching the spec and API) rather than `double`.

### Fixed — robustness & DX

- Non-JSON or malformed response bodies no longer escape as a raw `System.Text.Json.JsonException`.
  A new **`SerializationException`** (deriving from `AssinafyException`) wraps unparseable 2xx bodies;
  unsuccessful responses surface as `ApiException`, so `catch (AssinafyException)` covers all
  SDK-specific API, transport, serialization, and validation failures.
- **DI:** `AssinafyClientOptions.Timeout` is now honored on the `AddAssinafy` path (it was previously
  ignored because the factory-created `HttpClient` kept the 100s default).
- Added enum-value constant classes (`AssignmentMethods`, `SignerChannels`, `AccountNotificationSenderTypes`)
  and centralized the signer-access-code query construction (DRY).

### Documentation

- Every public type, member, method parameter, and exception now carries XML documentation.
  `CS1591` is enforced as an error (with `TreatWarningsAsErrors`), so the API reference stays complete.

### Build / CI

- Publish workflow now pushes to **NuGet.org** (gated on a `NUGET_API_KEY` secret) in addition to
  GitHub Packages, and attaches build-provenance attestation.
- CI gains a least-privilege `permissions` block and `concurrency` cancellation; a secret-gated
  nightly **live-integration** workflow was added; Dependabot keeps Actions and NuGet deps current.
- `LangVersion` pinned to `12.0` and symbols embedded (with SourceLink) for reproducible, debuggable builds.

## 1.2.1

### Removed / changed (read before upgrading)

- **Removed `Webhooks.DeleteAsync`.** `DELETE /webhooks/subscriptions` is not part of
  the API. Use `Webhooks.InactivateAsync()` (or `UpdateSubscriptionAsync`
  with `IsActive = false`) to stop deliveries.
- **Removed `Assignment.Expiration`.** The API only ever returns/accepts `expires_at`
  and ignores `expiration`. Read `Assignment.ExpiresAt` instead.
- **`AssignmentEntryField.DisplaySettings` is now `object?`** (was `JsonElement?`) so
  callers can pass an anonymous object such as `new { x = 100, y = 200 }`. Assigning a
  `JsonElement` still compiles; only reading the property directly into a `JsonElement`
  now needs an explicit cast.

### Fixed

- `Documents.GetSigningProgressAsync` and `IsFullySignedAsync` now
  fall back to the per-signer `completed` flags when an assignment carries no
  `summary`, instead of always reporting `0` signed / `0%`.
- **New — signing order:** `SignerRef` and `TemplateSigner` now expose `Step`,
  the documented signing-order step, so assignment-create and create-from-template
  can request sequential signing.
- **New:** `UploadAndRequestSignaturesAsync` accepts an assignment `Method` and
  per-signer `VerificationMethod` / `NotificationMethods` / `Step`, instead of
  hardcoding `virtual` with id-only signers.
- **Security / robustness:** authentication is attached per request rather than to
  the shared `HttpClient.DefaultRequestHeaders`, so a caller-supplied client is
  never mutated and stays safe to reuse/share. Supplying both `ApiKey` and `Token`
  now throws a `ValidationException` instead of silently preferring the key. The DI
  registration recycles connections via `SocketsHttpHandler.PooledConnectionLifetime`
  (the captured singleton client cannot rely on factory handler rotation).
- **Robustness:** `FindByEmailAsync` pages through all result pages (it previously
  scanned only the first 100 and could miss an exact match); `SignMultipleAsync` /
  `DeclineMultipleAsync` reject an empty document list; `WaitUntilReadyAsync`
  tolerates a brief `404` immediately after creation; `ResetPasswordAsync` accepts the
  API's optional reset token; `UpdateSubscriptionAsync` guards a null `events`;
  numeric values coerced to string preserve their exact token text.
- **Cleanup (DRY / KISS):** removed the dead `Assignment.Expiration` property
  (the API only uses `expires_at`) and the redundant `signer_ids` request field;
  `DocumentDetails`/`TemplateDetails` extend their list counterparts and document
  and template pages share a `PageBase`; exception leaf types are `sealed`.
- **Packaging:** deterministic / CI builds for reproducible symbol packages.
- Coverage expanded to 91 unit tests plus an opt-in `LiveIntegrationTests` suite.

## 1.1.1

- **Packaging / CI:** added a GitHub Packages publish workflow and symbol
  (`snupkg`) packages. No library or API-surface changes.

## 1.1.0

- **New: Tags.** Added `client.Tags` (`TagResource`) covering the complete tag surface:
  - Workspace tags: `ListAsync` (with `search`), `CreateAsync`,
    `UpdateAsync`, `DeleteAsync` (with `force`).
  - Document tags: `AddToDocumentAsync` (append), `SetForDocumentAsync`
    (replace / clear), `ListForDocumentAsync`, `RemoveFromDocumentAsync`.
  - Added the `Tag`, `CreateTagRequest`, and `UpdateTagRequest` models.
- **Model fidelity** (additive, backward-compatible):
  - `DocumentListItem` and `DocumentDetails` now expose the `tags` array
    returned by the API.
  - `AssignmentCostEstimate` now includes the documented `blocking_reason`
    and `message` fields.
  - `AssignmentSigner` now includes `step` (signing order), `notified`, and
    `notification_history`.
- Added regression tests for the tag resource and CPF field validation.

## 1.0.1

- **Bug fix** (data loss): `CreateSignerRequest`, `UpdateSignerRequest`,
  `ConfirmSignerDataRequest`, and `UploadAndRequestSignaturesSigner` now
  serialize `WhatsAppPhoneNumber` as `whatsapp_phone_number` instead of
  the incorrect `whats_app_phone_number` produced by the default snake_case
  policy. Phone numbers passed to those endpoints were previously silently
  dropped by the API.
- **Bug fix**: `SignAssignmentValue` now serializes its keys as
  `itemId`, `fieldId`, `pageId`, `value` (camelCase). Per the Assinafy
  docs the Sign endpoint is the one place the API expects camelCase,
  unlike the rest of the surface which uses snake_case.
- Removed `DocumentResource.UpdateAsync` (and the orphaned
  `UpdateDocumentRequest` model) and `DocumentResource.GetAssignmentsAsync`.
  Both targeted endpoints (`PUT /documents/{id}` and
  `GET /documents/{id}/assignments`) are not part of the documented API
  and return `404` on the live service.
- Added regression tests covering the snake_case/camelCase fixes above.

## 1.0.0

- Aligned the SDK surface with the documented Assinafy API at `https://api.assinafy.com.br/v1/docs`.
- Added documented resources for authentication, fields, public documents, signer-facing signing flows, and signature images.
- Fixed request URI handling so the `/v1` base path is preserved for all relative SDK requests.
- Removed undocumented workspace CRUD and webhook HMAC verifier helpers.
- Removed Docker-only repository scaffolding; tests run with the .NET SDK via `dotnet test Assinafy.Sdk.sln`.
- Updated models for documented document, signer, assignment, template, field, webhook, authentication, and public document response shapes.
- Added focused tests for the documented SDK resources and HTTP request serialization.
