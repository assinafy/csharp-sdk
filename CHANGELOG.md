# Changelog

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
