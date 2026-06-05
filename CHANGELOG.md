# Changelog

## 1.2.1

All changes were verified end-to-end against the live API
(`https://sandbox.assinafy.com.br/v1`) during a full file-by-file audit.

### Removed / changed (read before upgrading)

- **Removed `Webhooks.DeleteAsync`.** `DELETE /webhooks/subscriptions` is not part of
  the API and returns `404 "Página não encontrada."` on the live service, so the method
  could never succeed. Use `Webhooks.InactivateAsync()` (or `UpdateSubscriptionAsync`
  with `IsActive = false`) to stop deliveries.
- **Removed `Assignment.Expiration`.** The API only ever returns/accepts `expires_at`
  (proven live: a create body using `expiration` is silently ignored). Read
  `Assignment.ExpiresAt` instead.
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
  tolerates a brief `404` immediately after creation; `ResetPasswordAsync` now
  requires its reset token; `UpdateSubscriptionAsync` guards a null `events`;
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

- **New: Tags.** Added `client.Tags` (`TagResource`) covering the full
  documented tag surface, all verified live against
  `https://api.assinafy.com.br/v1`:
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
- Added regression tests for the tag resource (now 73 tests total) and
  re-verified the full surface end-to-end against the production API,
  including CPF field validation.

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
- Verified the SDK end-to-end against `https://api.assinafy.com.br/v1`
  across documents, signers, fields, templates, webhooks, and
  authentication.

## 1.0.0

- Aligned the SDK surface with the documented Assinafy API at `https://api.assinafy.com.br/v1/docs`.
- Added documented resources for authentication, fields, public documents, signer-facing signing flows, and signature images.
- Fixed request URI handling so the `/v1` base path is preserved for all relative SDK requests.
- Removed undocumented workspace CRUD and webhook HMAC verifier helpers.
- Removed Docker-only repository scaffolding; tests run with the .NET SDK via `dotnet test Assinafy.Sdk.sln`.
- Updated models for documented document, signer, assignment, template, field, webhook, authentication, and public document response shapes.
- Added focused tests for the documented SDK resources and HTTP request serialization.
