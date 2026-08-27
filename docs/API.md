# Assinafy API and SDK reference

This reference combines the checked-in official production OpenAPI snapshot with the SDK's public resource methods. The OpenAPI snapshot is the canonical full HTTP payload contract; source XML comments document .NET parameters and behavior.

The checked-in production snapshot uses low-entropy `example-*` credential and token placeholders and RFC-reserved `example.com` email addresses. Its API structure and schemas match the production contract.

- Source: https://api.assinafy.com.br/v1/docs/openapi.json
- Snapshot SHA-256: `74f07116869697c5dfe439cfc860ed5203a7b914e99e6838f1959d3485ff5bb9`
- OpenAPI: `3.0.0`; API info version: `1.0.0`
- API surface: 89 documented production operations across 67 paths and 39 component schemas.

The API wraps JSON successes and errors as `{"status": number, "message": string|null, "data": ...}`. Binary routes return raw bytes. Authenticated routes accept a bearer token or `X-Api-Key`; signer routes use the `signer-access-code` query parameter; public routes deliberately receive no configured SDK credential.

## Official integration guidance

### Quick Start

Steps to quickly start using the API. By the end you'll be able to create a document and invite signers.

### 1. Create a user account

During development, create a user account in the **sandbox** (https://app-sandbox.assinafy.com.br). For integrations using an API key, create a *separate* user account (e.g. *My App*) so you can grant it only the access it needs. Use the production app (https://app.assinafy.com.br) once you go live.

### 2. Set up credentials

Generate an **API key** from the *My Account → API* page in the Assinafy app, and note your **workspace account ID** from *My Account → Workspaces*. Use the API key only from a back-end system — never store it in client-side code.

For sandbox plan/credit purchases, use the test card `5510 3647 0363 3414`.

### 3. Upload a document

```shell
curl -X POST "https://api.assinafy.com.br/v1/accounts/{account_id}/documents" \\
  -H 'X-Api-Key: {api_key}' \\
  -F 'file=@/tmp/document.pdf'
```

Save the returned document `id` for the next steps.

### 4. Create signers

```shell
curl -X POST "https://api.assinafy.com.br/v1/accounts/{account_id}/signers" \\
  -H 'X-Api-Key: {api_key}' \\
  -H 'Content-Type: application/json' \\
  -d '{ "full_name": "John Dove", "email": "john@example.com" }'
```

Save each signer `id`.

### 5. Request signatures

```shell
curl -X POST "https://api.assinafy.com.br/v1/documents/{document_id}/assignments" \\
  -H 'X-Api-Key: {api_key}' \\
  -H 'Content-Type: application/json' \\
  -d '{ "method": "virtual", "signers": [{ "id": "{signer_id}" }] }'
```

The `virtual` method requires no input from the signer. To collect input fields, use the `collect` method — see the Assignment section.

### Authentication

Authentication can be done in three ways:

- API key in the `X-Api-Key` header: `X-Api-Key: {api-key}`
- Access token in the `Authorization` header: `Authorization: Bearer {access-token}`
- Access token as a URL parameter: `?access-token={access-token}`

The **recommended** method is an API key, which you create from the settings page in the Assinafy app. An access token is obtained through login with email and password; it is a JWT that usually expires in one hour.

Avoid query-string access tokens in server integrations because URLs may be retained in browser history, proxy logs, and monitoring systems. The SDK sends API keys and bearer tokens in request headers.

### Accounts

Workspace accounts: profile, logo and theme.

### Signers

Manage the signing parties of a workspace.

### Documents

Create, list, download, tag and delete documents.

### Verification & Notification Methods

When you create an assignment, each signer can be configured with a **verification method** (how the signer proves their identity before signing) and one or more **notification methods** (how the signer is told a signature is being requested). These are set per signer via `signers[].verification_method` and `signers[].notification_methods` on the **Create assignment** endpoint.

For direct assignment creation, verification and notification are independently configurable. An omitted verification method defaults to `Email`; omitted notification methods default to `Email`. The create-from-template route accepts one notification method per signer and infers a missing verification or notification method from the supplied side.

### Verification methods

| Code | Description |
|------|-------------|
| `Email` | The signer receives a verification code by email that must be entered before signing. |
| `Whatsapp` | The signer receives a verification code over WhatsApp that must be entered before signing. |
| `DigitalCertificate` | The signer signs with their own ICP-Brasil digital certificate (A1/A3) from their device using the Web PKI browser extension, producing a qualified PAdES signature on the document. |

| Code | Requirements | Cost (per signer) |
|------|--------------|-------------------|
| `Email` | Signer must have an email address. | Free |
| `Whatsapp` | Signer must have a `whatsapp_phone_number`; available only on paid subscriptions. | Free |
| `DigitalCertificate` | Account must have the **Digital Certificate** feature (Standard and Pro plans). Signer must have a CPF in `government_id`. Each digital-certificate signer must be **alone in its signing step**. | 2 credits |

> **Digital Certificate cost.** Unlike the other verification methods (whose only cost is the notification), the digital-certificate signature itself is charged **2 credits per digital-certificate signer**, on top of the notification cost. The charge is applied when the assignment is created, and appears in the **Estimate assignment cost** breakdown under the `SignatureDigitalCertificate` code.

### Notification methods

The notification method is what delivers the signing invitation to the signer. Each notification has a credit cost charged when the assignment is created (and again when a notification is resent).

| Code | Description |
|------|-------------|
| `Email` | Sends an email invitation with a link to sign the document. |
| `Whatsapp` | Sends a WhatsApp message with a link to sign the document. |

| Code | Requirements | Cost (per signer) |
|------|--------------|-------------------|
| `Email` | Signer must have an email address. | 0 credits |
| `Whatsapp` | Signer must have a `whatsapp_phone_number`; available only on paid subscriptions. | 0.45 credits |

### Channel rules

Direct assignment creation accepts `Email`, `Whatsapp`, or both notification channels for a signer. Create-from-template accepts one notification channel per signer. Email and WhatsApp recipient requirements still apply, and WhatsApp requires a paid subscription.

### Default behavior

- **Direct assignment:** omitted `verification_method` defaults to `Email`; omitted `notification_methods` defaults to `Email`.
- **Create from template:** when only one side is supplied, the missing verification or notification method is inferred from it; when neither is supplied, both default to `Email`.

### Costs

Notification cost is charged **per signer**, on top of the document cost. For example:

- 2 signers notified by email = 0 credits
- 2 signers notified by WhatsApp = 0.9 credits

Use the **Estimate assignment cost** endpoint to preview the exact total (documents + notifications) before creating an assignment.

### Notification timing and sequential signing

By default every signer is notified as soon as the assignment is created. When you set `signers[].step` to define a signing order, each signer's notification is held until their step is activated: step 1 is notified at creation, and a later step is notified only after every signer in the previous step has finished.

### Assignments

Request signatures on a document and estimate their cost.

### Signing

Signer-facing endpoints: view, sign and decline documents using a signer access code.

### Templates

Reusable document templates, their roles, fields and tags.

### Fields

Reusable field definitions used on templates and assignments.

### Tags

Workspace-scoped labels attachable to documents and templates.

### Webhooks

Outbound webhook subscriptions and delivery history.

### Webhook Payloads

This section documents the HTTP request your endpoint receives whenever a subscribed event is triggered, and the shape of the JSON body sent for each event type.

### Delivery contract

When an event occurs, we send an HTTP `POST` request to the endpoint configured in your webhook subscription.

| Property | Value |
|----------|-------|
| Method | `POST` |
| Content-Type | `application/json` |
| Connection header | `close` |
| Success criteria | Any `2xx` response |
| Attempts | Up to 2 per event (initial attempt + 1 retry) |
| Retry wait | 3 seconds between attempts |
| Circuit breaker | After 10 consecutive failed events, delivery is paused and only ~5% of events are probed until one succeeds. Use the **Retry webhook delivery** endpoint to force redelivery. |
| Response capture | We store the first 2000 characters of your response body for debugging (see **List webhook deliveries**). |

Your endpoint must respond within a reasonable time and return a `2xx` status. Non-`2xx` responses, connection errors and timeouts are all treated as failed deliveries and count toward the circuit breaker.

### Common envelope

Every webhook body shares the same top-level structure:

| Field | Type | Description |
|-------|------|-------------|
| `id` | integer | Internal activity ID that produced this event. Useful for deduplication on your side. |
| `event` | string | Event type identifier. See the catalog below and the **List webhook event types** endpoint. |
| `message` | string \\| null | Human-readable message describing the event. May contain token placeholders. Can be `null`. |
| `payload` | object \\| null | Event-specific parameters. Keys vary per event — see the catalog. Can be `null`. |
| `origin` | object \\| null | Where the action was triggered from, when available: `{ ip, user-agent }`. |
| `created_at` | integer | Unix timestamp (seconds) when the event was recorded. |
| `subject` | object | The entity that performed the action. Polymorphic — see below. |
| `object` | object | The entity the action was performed on. Polymorphic — see below. |
| `account_id` | string | The ID of the account that owns this event. Always present. |

### About `subject` and `object`

Both are **polymorphic**: their shape depends on which entity they represent. We always add a `type` property with the entity class name, one of `User`, `Signer`, `Account`, `Document`, or `Template`. The remaining properties match the same fields the REST API returns for that resource. The `object` is serialized with its extra relationships expanded (e.g. a `Document` includes `assignment` and `pages`); the `subject` carries its base fields only. Whenever `subject` or `object` is an `Account`, the `integration` property (which includes dispatch history) is removed before sending.

For `document_ready`, `document_processing_failed` and `template_processing_failed`, the `subject` is the `Account` itself — these events are emitted by the system when processing finishes, not by a particular user.

### Event catalog

| Event | Subject | Object | `payload` keys | Description |
|-------|---------|--------|----------------|-------------|
| `document_uploaded` | User | Document | — | The user uploaded a new document. |
| `document_metadata_ready` | User | Document | — | The document was normalized to PDF and its pages are available. |
| `document_prepared` | User | Document | — | The user prepared a document (fields assigned to signers). |
| `assignment_created` | User | Document | `user_name`, `user_email`, `user_telephone` | The user created an assignment for the document. Includes a snapshot of the creator profile. |
| `document_ready` | Account | Document | — | The last signer signed the document; status is now `ready`. |
| `document_processing_failed` | Account | Document | `error_message` | The document could not be processed. |
| `signature_requested` | User | Document | `signer_email` / `signer_full_name` / `signer_whatsapp_phone_number` (depending on notification method) | The user requested a signer to sign the document. |
| `signer_created` | User | Signer | `signer_full_name` | The user created a new signer. |
| `signer_email_verified` | Signer | Document | `signer_email` | The signer's email was verified through a code linked to the document. |
| `signer_whatsapp_verified` | Signer | Document | `signer_whatsapp_phone_number` | The signer's WhatsApp number was verified through a code linked to the document. |
| `signer_data_confirmed` | Signer | Document | `signer_email` | The signer confirmed their data before signing. |
| `signer_viewed_document` | Signer | Document | `signer_full_name` | The signer opened the document for the first time. |
| `signer_signed_document` | Signer | Document | `signer_full_name` | The signer signed the document. |
| `signer_rejected_document` | Signer | Document | `signer_full_name` | The signer refused to sign the document. |
| `user_rejected_document` | User | Document | `user_name` | The document was cancelled by a user of the account. |
| `template_created` | User | Template | — | The user created a new template. |
| `template_processed` | User | Template | — | A template's metadata was processed and is ready for use. |
| `template_processing_failed` | Account | Template | `error_message` | A template could not be processed. |

`assignment_created` and `document_metadata_ready` have no guaranteed ordering: under the virtual pre-metadata flow, `assignment_created` may fire before `document_metadata_ready`. Treat any unknown field as a forward-compatible addition.

### Users

User account endpoints.

## SDK method index

| Resource | Method | Full signature | Summary |
|---|---|---|---|
| AccountResource | `ListAsync` | `Task<IReadOnlyList<Account>> ListAsync(CancellationToken cancellationToken = default)` | GET /accounts — list the workspace accounts the authenticated user belongs to. |
| AccountResource | `CreateAsync` | `Task<Account> CreateAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)` | POST /accounts — create a new workspace account owned by the authenticated user. |
| AccountResource | `GetAsync` | `Task<Account> GetAsync(string? accountId = null, CancellationToken cancellationToken = default)` | GET /accounts/{account_id} — retrieve a workspace account the user belongs to. |
| AccountResource | `UpdateAsync` | `Task<Account> UpdateAsync(UpdateAccountRequest request, string? accountId = null, CancellationToken cancellationToken = default)` | PUT /accounts/{account_id} — update a workspace account's profile. Only non-null request properties are sent. |
| AccountResource | `DeleteAsync` | `Task DeleteAsync(bool force = false, string? accountId = null, CancellationToken cancellationToken = default)` | DELETE /accounts/{account_id} — delete a workspace account. When force is false (default) the API returns 400 with a restrictions list if the workspace has an active paid subscription; pass true to cancel any active subscription automatically and delete immediately. |
| AccountResource | `GetThemeAsync` | `Task<AccountTheme> GetThemeAsync(string? accountId = null, CancellationToken cancellationToken = default)` | GET /accounts/{account_id}/theme — retrieve the account's branding theme (name, colors, and logo URL). |
| AccountResource | `GetStatsAsync` | `Task<IReadOnlyList<DocumentStatsRow>> GetStatsAsync(DocumentStatsParams? parameters = null, string? accountId = null, CancellationToken cancellationToken = default)` | GET /accounts/{account_id}/stats — retrieve the account's document KPI series. |
| AccountResource | `DownloadLogoAsync` | `Task<byte[]> DownloadLogoAsync(string? accountId = null, CancellationToken cancellationToken = default)` | GET /accounts/{account_id}/logo — download the account logo image binary. Throws 404 when no logo is set. |
| AccountResource | `UploadLogoAsync` | `Task UploadLogoAsync(Stream imageStream, string fileName, string contentType = "image/png", string? accountId = null, CancellationToken cancellationToken = default)` | POST /accounts/{account_id}/logo — upload or replace the account logo image (multipart file field). |
| AccountResource | `DeleteLogoAsync` | `Task DeleteLogoAsync(string? accountId = null, CancellationToken cancellationToken = default)` | DELETE /accounts/{account_id}/logo — remove the account logo image. |
| AssignmentResource | `ListAsync` | `Task<PaginatedResult<Assignment>> ListAsync(AssignmentListParams? parameters = null, string? accountId = null, CancellationToken cancellationToken = default)` | GET /assignments — list assignments for the authenticated user's current account. Passing accountId explicitly sends the legacy, undocumented accountId compatibility extension; the configured client default is not sent. |
| AssignmentResource | `CreateAsync` | `Task<Assignment> CreateAsync(string documentId, CreateAssignmentRequest request, CancellationToken cancellationToken = default)` | POST /documents/{document_id}/assignments — create a signature assignment binding signers to a document. |
| AssignmentResource | `EstimateCostAsync` | `Task<AssignmentCostEstimate> EstimateCostAsync(string documentId, CreateAssignmentRequest request, CancellationToken cancellationToken = default)` | POST /documents/{documentId}/assignments/estimate-cost — preview the credit cost of CreateAsync before committing. |
| AssignmentResource | `ResetExpirationAsync` | `Task<Assignment> ResetExpirationAsync(string documentId, string assignmentId, string? expiresAt, CancellationToken cancellationToken = default)` | PUT /documents/{documentId}/assignments/{assignmentId}/reset-expiration — set or clear the assignment's expiration date. |
| AssignmentResource | `ResendNotificationAsync` | `Task<ResendNotificationResult> ResendNotificationAsync(string documentId, string assignmentId, string signerId, CancellationToken cancellationToken = default)` | PUT /documents/{documentId}/assignments/{assignmentId}/signers/{signerId}/resend — resend the signature notification (email or WhatsApp) for a specific signer. |
| AssignmentResource | `EstimateResendCostAsync` | `Task<ResendCostEstimate> EstimateResendCostAsync(string documentId, string assignmentId, string signerId, CancellationToken cancellationToken = default)` | POST /documents/{documentId}/assignments/{assignmentId}/signers/{signerId}/estimate-resend-cost — preview the credit cost of ResendNotificationAsync. |
| AssignmentResource | `ListWhatsAppNotificationsAsync` | `Task<IReadOnlyList<WhatsAppNotification>> ListWhatsAppNotificationsAsync(string documentId, string assignmentId, CancellationToken cancellationToken = default)` | GET /documents/{documentId}/assignments/{assignmentId}/whatsapp-notifications — list the rendered WhatsApp template messages dispatched for an assignment. |
| AuthenticationResource | `LoginAsync` | `Task<AuthenticationResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)` | POST /login — exchange email and password for an access token and account list. |
| AuthenticationResource | `SocialLoginAsync` | `Task<AuthenticationResult> SocialLoginAsync(SocialLoginRequest request, CancellationToken cancellationToken = default)` | POST /authentication/social-login — exchange a third-party provider token (e.g. Google) for an Assinafy access token. |
| AuthenticationResource | `LinkSocialLoginAsync` | `Task LinkSocialLoginAsync(LinkSocialLoginRequest request, CancellationToken cancellationToken = default)` | POST /auth/link-social-login — link a social-login provider (e.g. Google) to the currently authenticated user. The API accepts either a bearer token or an API key. |
| AuthenticationResource | `CreateApiKeyAsync` | `Task<ApiKeyResult> CreateApiKeyAsync(CreateApiKeyRequest request, CancellationToken cancellationToken = default)` | POST /users/api-keys — generate a personal API key. Replaces any previous key for the user. |
| AuthenticationResource | `GetApiKeyAsync` | `Task<ApiKeyResult> GetApiKeyAsync(CancellationToken cancellationToken = default)` | GET /users/api-keys — fetch a masked representation of the user's current API key. |
| AuthenticationResource | `DeleteApiKeyAsync` | `Task DeleteApiKeyAsync(CancellationToken cancellationToken = default)` | DELETE /users/api-keys — revoke the user's current API key. |
| AuthenticationResource | `ChangePasswordAsync` | `Task<EmailResult> ChangePasswordAsync(ChangePasswordRequest request, CancellationToken cancellationToken = default)` | PUT /authentication/change-password — change the user's password while authenticated. |
| AuthenticationResource | `RequestPasswordResetAsync` | `Task<EmailResult> RequestPasswordResetAsync(RequestPasswordResetRequest request, CancellationToken cancellationToken = default)` | PUT /authentication/request-password-reset — email the user a password reset token. |
| AuthenticationResource | `ResetPasswordAsync` | `Task<EmailResult> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default)` | PUT /authentication/reset-password — set a new password; the API accepts an optional token from RequestPasswordResetAsync. |
| DocumentResource | `ListStatusesAsync` | `Task<IReadOnlyList<DocumentStatusInfo>> ListStatusesAsync(CancellationToken cancellationToken = default)` | GET /documents/statuses — list all possible document status codes and whether each is deletable. |
| DocumentResource | `UploadAsync` | `Task<DocumentDetails> UploadAsync(Stream fileStream, string fileName, string? accountId = null, CancellationToken cancellationToken = default)` | POST /accounts/{account_id}/documents — upload a PDF to a workspace. The SDK validates the extension and the remaining length of seekable streams against 25MB; the API enforces size for every stream and a 2000-page limit. |
| DocumentResource | `ListAsync` | `Task<PaginatedResult<DocumentListItem>> ListAsync(IDictionary<string, string?>? queryParams = null, string? accountId = null, CancellationToken cancellationToken = default)` | GET /accounts/{account_id}/documents — list documents in the workspace with optional filters (status, method, search, sort, page, per-page). |
| DocumentResource | `GetAsync` | `Task<DocumentDetails> GetAsync(string documentId, CancellationToken cancellationToken = default)` | GET /documents/{document_id} — fetch full document details including assignment and artifacts. |
| DocumentResource | `DeleteAsync` | `Task DeleteAsync(string documentId, CancellationToken cancellationToken = default)` | DELETE /documents/{documentId} — delete a document. Only certain status codes are deletable (see ListStatusesAsync). |
| DocumentResource | `RenameAsync` | `Task<DocumentDetails> RenameAsync(string documentId, string name, CancellationToken cancellationToken = default)` | PATCH /documents/{document_id} — rename a document. Only permitted before any assignment exists (status uploaded or metadata_ready with no signers); once signing has started or the document is certificated the name is locked. The server normalizes the name (diacritics are removed and unsupported characters are replaced with dashes), so the returned name may differ. |
| DocumentResource | `SearchAsync` | `Task<PaginatedResult<DocumentListItem>> SearchAsync(string? search = null, string? status = null, int? page = null, int? perPage = null, string? accountId = null, CancellationToken cancellationToken = default)` | GET /accounts/{account_id}/documents/search — search documents by name, returning a compact representation (without the expanded assignment and pages that ListAsync includes). Use ListAsync when you need the full document shape. |
| DocumentResource | `DownloadAsync` | `Task<byte[]> DownloadAsync(string documentId, string artifactName = DocumentArtifactNames.Certificated, CancellationToken cancellationToken = default)` | GET /documents/{document_id}/download/{artifact_name} — download a document artifact. |
| DocumentResource | `ThumbnailAsync` | `Task<byte[]> ThumbnailAsync(string documentId, CancellationToken cancellationToken = default)` | GET /documents/{document_id}/thumbnail — download the first-page thumbnail image. |
| DocumentResource | `DownloadPageAsync` | `Task<byte[]> DownloadPageAsync(string documentId, string pageId, CancellationToken cancellationToken = default)` | GET /documents/{document_id}/pages/{page_id}/download — download a single page rendering. |
| DocumentResource | `ActivitiesAsync` | `Task<IReadOnlyList<DocumentActivity>> ActivitiesAsync(string documentId, CancellationToken cancellationToken = default)` | GET /documents/{documentId}/activities — fetch the timeline of events recorded against this document. |
| DocumentResource | `WaitUntilReadyAsync` | `Task<DocumentDetails> WaitUntilReadyAsync(string documentId, TimeSpan? maxWait = null, TimeSpan? pollInterval = null, CancellationToken cancellationToken = default)` | Convenience helper: poll GetAsync until the document reaches a "ready" status (metadata_ready, pending_signature, or certificated), throws if it lands in a failed/expired state, or throws on timeout. |
| DocumentResource | `IsFullySignedAsync` | `Task<bool> IsFullySignedAsync(string documentId, CancellationToken cancellationToken = default)` | Convenience helper: returns true if the document is fully signed by every signer. |
| DocumentResource | `GetSigningProgressAsync` | `Task<SigningProgress> GetSigningProgressAsync(string documentId, CancellationToken cancellationToken = default)` | Convenience helper: returns a (signed / total / pending / percentage) snapshot. |
| DocumentResource | `CreateFromTemplateAsync` | `Task<DocumentDetails> CreateFromTemplateAsync(string templateId, IReadOnlyList<TemplateSigner> signers, CreateDocumentFromTemplateOptions? options = null, string? accountId = null, CancellationToken cancellationToken = default)` | POST /accounts/{account_id}/templates/{template_id}/documents — create a document by binding signers to template roles. |
| DocumentResource | `EstimateCostFromTemplateAsync` | `Task<AssignmentCostEstimate> EstimateCostFromTemplateAsync(string templateId, IReadOnlyList<TemplateSigner> signers, string? accountId = null, CancellationToken cancellationToken = default)` | POST /accounts/{account_id}/templates/{template_id}/documents/estimate-cost — preview the credit cost of CreateFromTemplateAsync. |
| DocumentResource | `VerifyAsync` | `Task<DocumentVerificationResult> VerifyAsync(string signatureHash, CancellationToken cancellationToken = default)` | GET /documents/{signature_hash}/verify — verify a document's signature hash and return validity metadata. |
| FieldResource | `CreateAsync` | `Task<FieldDefinition> CreateAsync(CreateFieldDefinitionRequest request, string? accountId = null, CancellationToken cancellationToken = default)` | POST /accounts/{accountId}/fields — create a workspace-scoped field definition. |
| FieldResource | `ListAsync` | `Task<PaginatedResult<FieldDefinition>> ListAsync(FieldListParams? parameters = null, string? accountId = null, CancellationToken cancellationToken = default)` | GET /accounts/{accountId}/fields — list field definitions. Use FieldListParams to include inactive or standard built-ins. |
| FieldResource | `GetAsync` | `Task<FieldDefinition> GetAsync(string fieldId, string? accountId = null, CancellationToken cancellationToken = default)` | GET /accounts/{accountId}/fields/{field_id} — fetch a single field definition. |
| FieldResource | `UpdateAsync` | `Task<FieldDefinition> UpdateAsync(string fieldId, UpdateFieldDefinitionRequest request, string? accountId = null, CancellationToken cancellationToken = default)` | PUT /accounts/{account_id}/fields/{field_id} — update a field definition. |
| FieldResource | `DeleteAsync` | `Task DeleteAsync(string fieldId, string? accountId = null, CancellationToken cancellationToken = default)` | DELETE /accounts/{account_id}/fields/{field_id} — delete a field definition. Will fail if the field has already been used on a document. |
| FieldResource | `ValidateAsync` | `Task<FieldValidationResult> ValidateAsync(string fieldId, ValidateFieldValueRequest request, string? signerAccessCode = null, string? accountId = null, CancellationToken cancellationToken = default)` | POST /accounts/{accountId}/fields/{field_id}/validate — validate a single value against a field definition. Pass a signer access code when calling on a signer's behalf. |
| FieldResource | `ValidateMultipleAsync` | `Task<IReadOnlyList<FieldValidationResult>> ValidateMultipleAsync(IReadOnlyList<ValidateFieldValueItem> values, string? signerAccessCode = null, string? accountId = null, CancellationToken cancellationToken = default)` | POST /accounts/{accountId}/fields/validate-multiple — validate multiple values at once. |
| FieldResource | `ListTypesAsync` | `Task<IReadOnlyList<FieldTypeInfo>> ListTypesAsync(CancellationToken cancellationToken = default)` | GET /field-types — list the platform-supported field input types. |
| PublicDocumentResource | `GetAsync` | `Task<PublicDocumentInfo> GetAsync(string documentId, CancellationToken cancellationToken = default)` | Legacy GET /public/documents/{document_id} projection retained for compatibility. Use GetDetailsAsync for the complete documented DocumentDetails payload. |
| PublicDocumentResource | `GetDetailsAsync` | `Task<DocumentDetails> GetDetailsAsync(string documentId, CancellationToken cancellationToken = default)` | GET /public/documents/{document_id} — fetch the complete public document payload (no authentication required). |
| PublicDocumentResource | `SendTokenAsync` | `Task SendTokenAsync(string documentId, string? email = null, CancellationToken cancellationToken = default)` | PUT /public/documents/{document_id}/send-token — ask the API to email a signing access token. |
| PublicDocumentResource | `SendTokenAsync` | `Task<SendDocumentTokenResult> SendTokenAsync(string documentId, SendDocumentTokenRequest request, CancellationToken cancellationToken = default)` | Legacy send-token overload retained for compatibility. The current API accepts only an optional email and returns no data. |
| SignatureResource | `UploadAsync` | `Task UploadAsync(Stream imageStream, string signerAccessCode, string type = SignatureImageTypes.Signature, string contentType = "image/png", CancellationToken cancellationToken = default)` | POST /signature?signer-access-code={code}&type={type} — upload the signer's signature or initial image. |
| SignatureResource | `UploadAsync` | `Task UploadAsync(Stream imageStream, string signerAccessCode, bool reuse, string type = SignatureImageTypes.Signature, string contentType = "image/png", CancellationToken cancellationToken = default)` | POST /signature?signer-access-code={code}&type={type}&reuse={reuse} — upload a signature or initial and choose whether it may be reused. |
| SignatureResource | `DownloadAsync` | `Task<byte[]> DownloadAsync(string signerAccessCode, string type = SignatureImageTypes.Signature, CancellationToken cancellationToken = default)` | GET /signature/{type}?signer-access-code={code} — download the signer's signature or initial image. |
| SignerResource | `CreateAsync` | `Task<Signer> CreateAsync(CreateSignerRequest request, string? accountId = null, CancellationToken cancellationToken = default)` | POST /accounts/{account_id}/signers — create a signer within the workspace. |
| SignerResource | `GetAsync` | `Task<Signer> GetAsync(string signerId, string? accountId = null, CancellationToken cancellationToken = default)` | GET /accounts/{account_id}/signers/{signer_id} — fetch a single signer's profile. |
| SignerResource | `ListAsync` | `Task<PaginatedResult<Signer>> ListAsync(IDictionary<string, string?>? queryParams = null, string? accountId = null, CancellationToken cancellationToken = default)` | GET /accounts/{account_id}/signers — list signers with optional search, page, and per-page filters. |
| SignerResource | `UpdateAsync` | `Task<Signer> UpdateAsync(string signerId, UpdateSignerRequest request, string? accountId = null, CancellationToken cancellationToken = default)` | PUT /accounts/{account_id}/signers/{signer_id} — update a signer. Verification integrity rules may block changing email or WhatsApp phone for in-flight signers. |
| SignerResource | `DeleteAsync` | `Task DeleteAsync(string signerId, string? accountId = null, CancellationToken cancellationToken = default)` | DELETE /accounts/{account_id}/signers/{signer_id} — remove a signer from the workspace. |
| SignerResource | `FindByEmailAsync` | `Task<Signer?> FindByEmailAsync(string email, string? accountId = null, CancellationToken cancellationToken = default)` | Convenience helper: page through ListAsync filtered by the email (a server-side fuzzy search) and return the first exact, case-insensitive email match across all result pages, or null if none exists. |
| SignerResource | `GetSelfAsync` | `Task<Signer> GetSelfAsync(string signerAccessCode, CancellationToken cancellationToken = default)` | GET /signers/self — signer-facing endpoint: load the signer's own profile using only an access code. |
| SignerResource | `AcceptTermsAsync` | `Task<Signer> AcceptTermsAsync(string signerAccessCode, CancellationToken cancellationToken = default)` | PUT /signers/accept-terms — record acceptance of the terms. The API returns no data; the returned Signer is a synthetic legacy-compatibility result. |
| SignerResource | `VerifyAsync` | `Task VerifyAsync(string signerAccessCode, string verificationCode, CancellationToken cancellationToken = default)` | POST /verify — verify an email or WhatsApp one-time code. The API returns no data. |
| SignerResource | `VerifyEmailAsync` | `Task<VerifyEmailResult> VerifyEmailAsync(string signerAccessCode, string verificationCode, CancellationToken cancellationToken = default)` | POST /verify — verify the emailed OTP. The API returns no data; the returned VerifyEmailResult is a synthetic legacy-compatibility result. |
| SignerResource | `ConfirmDataAsync` | `Task ConfirmDataAsync(string documentId, string signerAccessCode, ConfirmSignerDataRequest request, CancellationToken cancellationToken = default)` | PUT /documents/{document_id}/signers/confirm-data — signer-facing endpoint: confirm or supply the signer's full name, email, and government ID for a virtual assignment. Virtual assignments require this call to succeed before SigningResource.SignAsync. |
| SignerResource | `ConfirmDataWithResultAsync` | `Task<Signer> ConfirmDataWithResultAsync(string documentId, string signerAccessCode, ConfirmSignerDataRequest request, CancellationToken cancellationToken = default)` | PUT /documents/{document_id}/signers/confirm-data — confirm signer data and return the complete updated signer payload. |
| SigningResource | `GetAsync` | `Task<DocumentDetails> GetAsync(string signerAccessCode, bool? hasAcceptedTerms = null, CancellationToken cancellationToken = default)` | GET /sign — signer-facing endpoint: load the document and assignment data for the current signer access code. |
| SigningResource | `SignAsync` | `Task SignAsync(string documentId, string assignmentId, string signerAccessCode, IReadOnlyList<SignAssignmentValue> values, CancellationToken cancellationToken = default)` | POST /documents/{documentId}/assignments/{assignmentId}?signer-access-code={code} — submit a signer's field values. For virtual assignments the signer must call SignerResource.ConfirmDataAsync first; otherwise the API returns 400. The body uses camelCase keys (itemId, fieldId, pageId, value) per the Assinafy docs, which SignAssignmentValue applies automatically. |
| SigningResource | `StartCertificateAsync` | `Task<CertificateStartResult> StartCertificateAsync(string signerAccessCode, CancellationToken cancellationToken = default)` | Production-only deployed extension: POST /signers/certificate/start?signer-access-code={code} — start an ICP-Brasil certificate-signing operation and return its Web PKI token. This route is absent from the sandbox and published OpenAPI document. |
| SigningResource | `CompleteCertificateAsync` | `Task<CertificateCompleteResult> CompleteCertificateAsync(string signerAccessCode, string token, CancellationToken cancellationToken = default)` | Production-only deployed extension: POST /signers/certificate/complete?signer-access-code={code} — submit the signed Web PKI token and return the certificate signer name. This route is absent from the sandbox and published OpenAPI document. |
| SigningResource | `DeclineAsync` | `Task DeclineAsync(string documentId, string assignmentId, string signerAccessCode, string declineReason, CancellationToken cancellationToken = default)` | PUT /documents/{documentId}/assignments/{assignmentId}/reject?signer-access-code={code} — signer-facing endpoint: decline an assignment with a reason. |
| SigningResource | `GetCurrentDocumentAsync` | `Task<DocumentDetails> GetCurrentDocumentAsync(string signerId, string signerAccessCode, CancellationToken cancellationToken = default)` | GET /signers/{signer_id}/document?signer-access-code={code} — fetch the signer's current document. |
| SigningResource | `ListDocumentsAsync` | `Task<PaginatedResult<DocumentListItem>> ListDocumentsAsync(string signerId, string signerAccessCode, SignerDocumentListParams? parameters = null, CancellationToken cancellationToken = default)` | GET /signers/{signer_id}/documents?signer-access-code={code} — list all documents associated with the signer. |
| SigningResource | `SearchDocumentsAsync` | `Task<PaginatedResult<DocumentListItem>> SearchDocumentsAsync(string signerId, string signerAccessCode, SignerDocumentListParams? parameters = null, CancellationToken cancellationToken = default)` | GET /signers/{signer_id}/documents/search?signer-access-code={code} — search the signer's documents by name, returning a compact representation. Use ListDocumentsAsync when you need the full document shape or pagination. |
| SigningResource | `SignMultipleAsync` | `Task SignMultipleAsync(string signerAccessCode, IReadOnlyList<string> documentIds, CancellationToken cancellationToken = default)` | PUT /signers/documents/sign-multiple?signer-access-code={code} — sign multiple virtual-method documents in one request. |
| SigningResource | `DeclineMultipleAsync` | `Task DeclineMultipleAsync(string signerAccessCode, IReadOnlyList<string> documentIds, string declineReason, CancellationToken cancellationToken = default)` | PUT /signers/documents/decline-multiple?signer-access-code={code} — decline multiple documents at once with a single reason. |
| SigningResource | `DownloadPublicAsync` | `Task<byte[]> DownloadPublicAsync(string signerId, string documentId, string artifactName = DocumentArtifactNames.Certificated, CancellationToken cancellationToken = default)` | GET /signers/{signer_id}/documents/{document_id}/download/{artifact_name} — public download of a signer document artifact. |
| SigningResource | `DownloadAsync` | `Task<byte[]> DownloadAsync(string signerId, string documentId, string? signerAccessCode = null, string artifactName = DocumentArtifactNames.Certificated, CancellationToken cancellationToken = default)` | Retained public-download overload; its signer-access-code argument is not sent because this endpoint is public. |
| TagResource | `ListAsync` | `Task<IReadOnlyList<Tag>> ListAsync(string? search = null, string? accountId = null, CancellationToken cancellationToken = default)` | GET /accounts/{account_id}/tags — list workspace tags ordered alphabetically, optionally filtered by a case-insensitive search substring. |
| TagResource | `CreateAsync` | `Task<Tag> CreateAsync(CreateTagRequest request, string? accountId = null, CancellationToken cancellationToken = default)` | POST /accounts/{account_id}/tags — create a tag. The API returns 409 Conflict if the name already exists (case-insensitive). |
| TagResource | `UpdateAsync` | `Task<Tag> UpdateAsync(string tagId, UpdateTagRequest request, string? accountId = null, CancellationToken cancellationToken = default)` | PUT /accounts/{account_id}/tags/{tag_id} — update a tag's name and/or color. The API returns 409 Conflict if the new name collides with another tag. |
| TagResource | `DeleteAsync` | `Task DeleteAsync(string tagId, bool force = false, string? accountId = null, CancellationToken cancellationToken = default)` | DELETE /accounts/{account_id}/tags/{tag_id} — delete a tag. When force is false the API returns 409 Conflict if the tag is still attached to documents or templates; pass true to detach it everywhere and delete it. |
| TagResource | `DeleteWithResultAsync` | `Task<DeleteTagResult> DeleteWithResultAsync(string tagId, bool force = false, string? accountId = null, CancellationToken cancellationToken = default)` | DELETE /accounts/{account_id}/tags/{tag_id} — delete a tag and return the API's {"deleted":boolean} payload. |
| TagResource | `ListForDocumentAsync` | `Task<IReadOnlyList<Tag>> ListForDocumentAsync(string documentId, string? accountId = null, CancellationToken cancellationToken = default)` | GET /accounts/{account_id}/documents/{document_id}/tags — list the tags currently attached to a document. |
| TagResource | `AddToDocumentAsync` | `Task<IReadOnlyList<Tag>> AddToDocumentAsync(string documentId, IReadOnlyList<string> tags, string? accountId = null, CancellationToken cancellationToken = default)` | POST /accounts/{account_id}/documents/{document_id}/tags — attach existing tags to a document, keeping any already attached. |
| TagResource | `SetForDocumentAsync` | `Task<IReadOnlyList<Tag>> SetForDocumentAsync(string documentId, IReadOnlyList<string> tags, string? accountId = null, CancellationToken cancellationToken = default)` | PUT /accounts/{account_id}/documents/{document_id}/tags — replace a document's tags with exactly the supplied set (pass an empty list to clear all). |
| TagResource | `RemoveFromDocumentAsync` | `Task RemoveFromDocumentAsync(string documentId, string tagId, string? accountId = null, CancellationToken cancellationToken = default)` | DELETE /accounts/{account_id}/documents/{document_id}/tags/{tag_id} — detach a single tag from a document without deleting the tag itself. |
| TagResource | `RemoveFromDocumentWithResultAsync` | `Task<DetachTagResult> RemoveFromDocumentWithResultAsync(string documentId, string tagId, string? accountId = null, CancellationToken cancellationToken = default)` | DELETE /accounts/{account_id}/documents/{document_id}/tags/{tag_id} — detach a tag and return the API's {"detached":boolean} payload. |
| TemplateResource | `ListAsync` | `Task<PaginatedResult<TemplateListItem>> ListAsync(IDictionary<string, string?>? queryParams = null, string? accountId = null, CancellationToken cancellationToken = default)` | GET /accounts/{account_id}/templates — list templates in a workspace with optional search, page, and per-page filters. |
| TemplateResource | `CreateAsync` | `Task<TemplateDetails> CreateAsync(Stream fileStream, string fileName, string? name = null, string? accountId = null, CancellationToken cancellationToken = default)` | POST /accounts/{account_id}/templates — create a reusable template from one PDF multipart file part. |
| TemplateResource | `GetAsync` | `Task<TemplateDetails> GetAsync(string templateId, string? accountId = null, CancellationToken cancellationToken = default)` | GET /accounts/{account_id}/templates/{template_id} — fetch template details including roles, pages, and field placements. |
| TemplateResource | `UpdateAsync` | `Task<TemplateDetails> UpdateAsync(string templateId, UpdateTemplateRequest request, string? accountId = null, CancellationToken cancellationToken = default)` | PUT /accounts/{account_id}/templates/{template_id} — update the template name and/or default invitation message. |
| TemplateResource | `DeleteAsync` | `Task DeleteAsync(string templateId, string? accountId = null, CancellationToken cancellationToken = default)` | DELETE /accounts/{account_id}/templates/{template_id} — permanently delete a reusable template. |
| TemplateResource | `DownloadPageAsync` | `Task<byte[]> DownloadPageAsync(string templateId, string pageId, string? accountId = null, CancellationToken cancellationToken = default)` | GET /accounts/{account_id}/templates/{template_id}/pages/{page_id}/download — download a rendered template page as JPEG bytes. |
| UserResource | `GetSelfAsync` | `Task<UserProfile> GetSelfAsync(CancellationToken cancellationToken = default)` | GET /users/self — retrieve the authenticated user's profile. |
| UserResource | `GetNotificationPreferencesAsync` | `Task<NotificationPreferences> GetNotificationPreferencesAsync(CancellationToken cancellationToken = default)` | GET /users/self/notification-preferences — retrieve all nine email notification preferences. |
| UserResource | `UpdateNotificationPreferencesAsync` | `Task<NotificationPreferences> UpdateNotificationPreferencesAsync(UpdateNotificationPreferencesRequest request, CancellationToken cancellationToken = default)` | PUT /users/self/notification-preferences — merge selected email notification preferences and return the full updated map. |
| UserResource | `GetStatsAsync` | `Task<IReadOnlyList<DocumentStatsRow>> GetStatsAsync(DocumentStatsParams? parameters = null, CancellationToken cancellationToken = default)` | GET /users/self/stats — retrieve document KPIs summed across all accounts the authenticated user belongs to. |
| WebhookResource | `UpdateSubscriptionAsync` | `Task<WebhookSubscription> UpdateSubscriptionAsync(UpdateWebhookSubscriptionRequest request, string? accountId = null, CancellationToken cancellationToken = default)` | PUT /accounts/{account_id}/webhooks/subscriptions — create or replace the workspace's webhook subscription. |
| WebhookResource | `GetAsync` | `Task<WebhookSubscription> GetAsync(string? accountId = null, CancellationToken cancellationToken = default)` | GET /accounts/{account_id}/webhooks/subscriptions — fetch the workspace's current webhook subscription. API errors are propagated. |
| WebhookResource | `InactivateAsync` | `Task<WebhookSubscription> InactivateAsync(string? accountId = null, CancellationToken cancellationToken = default)` | PUT /accounts/{account_id}/webhooks/inactivate — pause delivery without losing the subscription configuration. The API has no delete endpoint; use this (or UpdateSubscriptionAsync with IsActive = false) to stop deliveries. |
| WebhookResource | `ListEventTypesAsync` | `Task<IReadOnlyList<WebhookEventTypeInfo>> ListEventTypesAsync(CancellationToken cancellationToken = default)` | GET /webhooks/event-types — list all event types supported by the platform. |
| WebhookResource | `ListDispatchesAsync` | `Task<PaginatedResult<WebhookDispatch>> ListDispatchesAsync(ListDispatchesParams? parameters = null, string? accountId = null, CancellationToken cancellationToken = default)` | GET /accounts/{account_id}/webhooks — list webhook delivery history with optional filters (event, delivered, from, to, page, per-page). |
| WebhookResource | `RetryDispatchAsync` | `Task<WebhookDispatch> RetryDispatchAsync(string dispatchId, string? accountId = null, CancellationToken cancellationToken = default)` | POST /accounts/{account_id}/webhooks/{dispatch_id}/retry — re-attempt delivery of a previous webhook dispatch. |

## C# client and helper contracts

### Client construction and lifetime

`AssinafyClientOptions` accepts `ApiKey` or `Token` (mutually exclusive), an optional default `AccountId`, an HTTPS `BaseUrl` whose path is exactly `/v1`, and a positive `Timeout` (or `Timeout.InfiniteTimeSpan`). `new AssinafyClient(options)` owns its transport and applies `Timeout`; `new AssinafyClient(options, httpClient)` leaves the supplied transport open, leaves its timeout unchanged, and requires its `BaseAddress` to match `BaseUrl`. A supplied API-key transport must have automatic redirects disabled.

`AssinafyClient.Create(apiKey, accountId, configure)` is the API-key shorthand. `AssinafyClient.FromConfig(config)` accepts `api_key`/`apiKey`, `account_id`/`accountId`, `token`/`access_token`/`accessToken`, and `base_url`/`baseUrl`. Dispose the client when it owns its transport; a supplied client's lifetime stays with its owner.

The package has no NuGet dependencies and ships no container adapter. For dependency injection, register a named `HttpClient` with `ConfigurePrimaryHttpMessageHandler(AssinafyClient.CreatePrimaryHandler)` and `SetHandlerLifetime(Timeout.InfiniteTimeSpan)`, then register `AssinafyClient` as a singleton over `IHttpClientFactory.CreateClient`. `AssinafyClient.CreatePrimaryHandler()` is public and returns the SDK's own handler: automatic redirects disabled, five-minute pooled connection lifetime. See the README for the full registration.

Every constructed client exposes `Authentication`, `Accounts`, `Users`, `Documents`, `Signers`, `Assignments`, `Templates`, `Tags`, `Fields`, `PublicDocuments`, `Signing`, `Signatures`, and `Webhooks`.

### SDK errors

- Malformed credentials and SDK-validated request values throw `ValidationException` before transport; required .NET arguments use `ArgumentNullException` or `ArgumentException`.
- A request body that cannot be encoded as JSON throws `SerializationException` before transport.
- HTTP/API-envelope failures throw `ApiException`, with status, message, and structured details when supplied.
- Transport failures and client-side request timeouts throw `NetworkException`.
- A successful response with an invalid envelope, missing required data, or incompatible JSON throws `SerializationException`.

### Upload-and-request helper

`UploadAndRequestSignaturesAsync` executes upload, optional readiness polling, signer creation, and assignment creation. Its request object is:

```csharp
new UploadAndRequestSignaturesOptions
{
    FileStream = pdf,                         // required Stream
    FileName = "contract.pdf",               // required PDF name
    AccountId = accountId,                    // optional account override
    WaitForReady = true,                      // default true
    Method = AssignmentMethods.Virtual,       // virtual or collect
    Message = "Please sign",                 // optional
    ExpiresAt = "2026-09-30T23:59:59Z",      // optional ISO-8601
    CopyReceivers = [copyReceiverSignerId],   // optional existing signer IDs
    Entries = null,                           // collect entries with known signer IDs
    EntriesFactory = signerIds =>             // or build entries from new signer IDs
    [
        new AssignmentEntry
        {
            PageId = "page-id",
            Fields =
            [
                new AssignmentEntryField
                {
                    SignerId = signerIds[0],
                    FieldId = "field-id",
                },
            ],
        },
    ],
    Signers =
    [
        new UploadAndRequestSignaturesSigner
        {
            FullName = "Example Signer",
            Email = "signer@example.com",
            WhatsAppPhoneNumber = null,
            VerificationMethod = SignerChannels.Email,
            NotificationMethods = [SignerChannels.Email],
            Step = 1,
        },
    ],
}
```

The response is `UploadAndRequestSignaturesResult`: `Document` is the uploaded `DocumentDetails`, `SignerIds` contains every created signer ID in request order, and `Assignment` is the created `Assignment`. The API has no transaction across these calls; successful earlier resources remain when a later request fails.

### Local convenience methods

| Method | Input | Result |
|---|---|---|
| `Documents.WaitUntilReadyAsync` | document ID, optional maximum wait and poll interval | The latest `DocumentDetails`; throws for terminal failure or timeout. |
| `Documents.IsFullySignedAsync` | document ID | `bool`, derived from assignment summary or signer completion flags. |
| `Documents.GetSigningProgressAsync` | document ID | `SigningProgress` with `Signed`, `Total`, `Pending`, and `Percentage`. |
| `Signers.FindByEmailAsync` | email and optional account ID | First exact case-insensitive `Signer`, or `null`; follows every server page. |
| `Dispose` | none | Disposes only an SDK-owned `HttpClient`; a supplied client remains usable. |

## Template and digital-certificate payloads

The signer certificate routes use only `signer-access-code` and never receive configured client credentials. They are production-only deployed extensions: the sandbox does not expose them, and they are not included in the published OpenAPI document. A valid production `DigitalCertificate` assignment and browser-signed Web PKI token are required for the complete flow.

### POST /v1/accounts/{accountId}/templates

Authentication: bearer token or `X-Api-Key`.

Request body: required `multipart/form-data` with exactly one `file` part containing a PDF. `TemplateResource.CreateAsync` uses an optional display name as the part filename and adds a `.pdf` suffix when needed; it does not send a second form field.

```http
Content-Disposition: form-data; name="file"; filename="Sales agreement.pdf"
Content-Type: application/pdf

<PDF bytes>
```

Success: `200` with a standard JSON envelope whose `data` is `TemplateDetails`. The create and update responses may omit `default_document_tags`; that property is returned by the GET-by-ID route.

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "template",
    "id": "template-id",
    "name": "Sales agreement.pdf",
    "document_name": null,
    "message": "Please review and sign",
    "status": "ready",
    "pages": [
      {
        "id": "page-id",
        "number": 1,
        "height": 1684,
        "width": 1191,
        "download_url": "https://example.com/template-page.jpg",
        "fields": [
          {
            "id": "placement-id",
            "field_id": "field-id",
            "role_id": "role-id",
            "label": "Signature",
            "display_settings": { "x": 100, "y": 200 },
            "created_at": "2026-08-26T12:00:00Z",
            "updated_at": null
          }
        ]
      }
    ],
    "roles": [
      {
        "id": "role-id",
        "name": "Signer",
        "assignment_type": "Signer",
        "created_at": "2026-08-26T12:00:00Z",
        "updated_at": null
      }
    ],
    "tags": [],
    "created_at": "2026-08-26T12:00:00Z",
    "updated_at": null
  }
}
```

Errors: invalid PDF uploads use the standard `400` JSON error envelope, missing or invalid credentials use `401`, and server failures use the standard JSON error envelope. The SDK rejects invalid PDF names, seekable payloads over 25 MB, and successful responses without a template ID before returning a result.

### GET /v1/accounts/{accountId}/templates/{templateId}

Authentication: bearer token or `X-Api-Key`.

Request body: none.

Success: `200` with the full `TemplateDetails` envelope shown above plus `data.default_document_tags`, an array of tags automatically applied to documents created from the template:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "template",
    "id": "template-id",
    "name": "Sales agreement.pdf",
    "document_name": null,
    "message": "Please review and sign",
    "status": "ready",
    "pages": [],
    "roles": [],
    "tags": [],
    "default_document_tags": [],
    "created_at": "2026-08-26T12:00:00Z",
    "updated_at": null
  }
}
```

Errors: missing or invalid credentials use the standard `401` JSON error envelope; an unknown template uses `404`; server failures use the standard JSON error envelope.

### PUT /v1/accounts/{accountId}/templates/{templateId}

Authentication: bearer token or `X-Api-Key`.

Request body: required `application/json` object containing `name`, `message`, or both. `TemplateResource.UpdateAsync` omits properties whose values are `null`.

```json
{
  "name": "Sales agreement v2",
  "message": "Please review and sign"
}
```

Success: `200` with the `TemplateDetails` envelope shown under POST. `default_document_tags` is optional on this response.

Errors: invalid properties use the standard `400` JSON error envelope, missing or invalid credentials use `401`, an unknown template uses `404`, and server failures use the standard JSON error envelope.

### DELETE /v1/accounts/{accountId}/templates/{templateId}

Authentication: bearer token or `X-Api-Key`.

Request body: none.

Success: `200` with a standard successful no-data envelope:

```json
{ "status": 200, "message": "", "data": null }
```

Errors: missing or invalid credentials use the standard `401` JSON error envelope; an unknown template uses `404`; server failures use the standard JSON error envelope.

### GET /v1/accounts/{accountId}/templates/{templateId}/pages/{pageId}/download

Authentication: bearer token or `X-Api-Key`.

Request body: none.

Success: `200` with raw `image/jpeg` bytes and no JSON envelope.

Errors: missing or invalid credentials use the standard `401` JSON error envelope; an unknown template or page uses `404`; server failures use the standard JSON error envelope instead of image bytes.

### POST /v1/signers/certificate/start

Authentication: signer access code. Configured bearer and API-key credentials are not sent.

Request body: required `application/json`. `SigningResource.StartCertificateAsync` sends the access code in both the query and body:

```http
POST /v1/signers/certificate/start?signer-access-code=access-code
Content-Type: application/json

{ "signer-access-code": "access-code" }
```

Success: `200` with a standard JSON envelope whose `data.token` is the Web PKI operation token:

```json
{ "status": 200, "message": "", "data": { "token": "web-pki-token" } }
```

Errors: an invalid or expired signer access code uses the standard `401` JSON error envelope; an invalid signing state or request uses `400`; server failures use the standard JSON error envelope.

### POST /v1/signers/certificate/complete

Authentication: signer access code. Configured bearer and API-key credentials are not sent.

Request body: required `application/json`. `SigningResource.CompleteCertificateAsync` sends the browser-signed token:

```http
POST /v1/signers/certificate/complete?signer-access-code=access-code
Content-Type: application/json

{ "signer-access-code": "access-code", "token": "signed-web-pki-token" }
```

Success: `200` with a standard JSON envelope whose `data.signerName` identifies the certificate signer:

```json
{ "status": 200, "message": "", "data": { "signerName": "Certificate Signer" } }
```

Errors: an invalid or expired signer access code uses the standard `401` JSON error envelope; an invalid signed token or signing state uses `400`; server failures use the standard JSON error envelope. For example: `{ "status": 400, "message": "Invalid request", "data": {} }`.

## Complete production operation matrix

| Tag | Method | Path | Request body | 2xx response | Security | Errors |
|---|---|---|---|---|---|---|
| Accounts | GET | `/v1/accounts/{accountId}` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 404, 500 |
| Accounts | PUT | `/v1/accounts/{accountId}` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 500 |
| Accounts | DELETE | `/v1/accounts/{accountId}` | application/json: object (optional) | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 404, 500 |
| Accounts | GET | `/v1/accounts/{accountId}/theme` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Accounts | GET | `/v1/accounts/{accountId}/logo` | none | image/*: string | bearerAuth or apiKeyAuth | 401, 404, 500 |
| Accounts | POST | `/v1/accounts/{accountId}/logo` | multipart/form-data: object (required) | application/json: Envelope | bearerAuth or apiKeyAuth | 400, 401, 500 |
| Accounts | DELETE | `/v1/accounts/{accountId}/logo` | none | application/json: Envelope | bearerAuth or apiKeyAuth | 401, 500 |
| Accounts | GET | `/v1/accounts` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Accounts | POST | `/v1/accounts` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 500 |
| Documents | GET | `/v1/documents/{documentId}/activities` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Assignments | GET | `/v1/assignments` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Assignments | POST | `/v1/documents/{documentId}/assignments` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 500 |
| Assignments | POST | `/v1/documents/{documentId}/assignments/estimate-cost` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 500 |
| Assignments | PUT | `/v1/documents/{documentId}/assignments/{assignmentId}/signers/{signerId}/resend` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Assignments | POST | `/v1/documents/{documentId}/assignments/{assignmentId}/signers/{signerId}/estimate-resend-cost` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Assignments | PUT | `/v1/documents/{documentId}/assignments/{assignmentId}/reset-expiration` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 404, 500 |
| Authentication | POST | `/v1/login` | application/json: object (required) | application/json: object | public | 400, 500 |
| Authentication | PUT | `/v1/authentication/request-password-reset` | application/json: object (required) | application/json: object | public | 500 |
| Authentication | PUT | `/v1/authentication/reset-password` | application/json: object (required) | application/json: object | public | 400, 500 |
| Authentication | PUT | `/v1/authentication/change-password` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 500 |
| Documents | GET | `/v1/accounts/{accountId}/documents` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Documents | POST | `/v1/accounts/{accountId}/documents` | multipart/form-data: object (required) | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 500 |
| Documents | GET | `/v1/accounts/{accountId}/documents/search` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Documents | GET | `/v1/documents/statuses` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Documents | GET | `/v1/documents/{documentId}` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 404, 500 |
| Documents | DELETE | `/v1/documents/{documentId}` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 404, 500 |
| Documents | PATCH | `/v1/documents/{documentId}` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 404, 500 |
| Documents | GET | `/v1/documents/{documentId}/download/{artifactName}` | none | application/pdf: string | bearerAuth or apiKeyAuth | 401, 404, 500 |
| Documents | GET | `/v1/documents/{documentSignatureHash}/verify` | none | application/json: object | public | 500 |
| Documents | GET | `/v1/accounts/{accountId}/documents/{documentId}/tags` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Documents | PUT | `/v1/accounts/{accountId}/documents/{documentId}/tags` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Documents | POST | `/v1/accounts/{accountId}/documents/{documentId}/tags` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Documents | DELETE | `/v1/accounts/{accountId}/documents/{documentId}/tags/{tagId}` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Fields | GET | `/v1/accounts/{accountId}/fields` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Fields | POST | `/v1/accounts/{accountId}/fields` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 500 |
| Fields | GET | `/v1/accounts/{accountId}/fields/{fieldId}` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 404, 500 |
| Fields | PUT | `/v1/accounts/{accountId}/fields/{fieldId}` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 401, 404, 500 |
| Fields | DELETE | `/v1/accounts/{accountId}/fields/{fieldId}` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 404, 500 |
| Fields | POST | `/v1/accounts/{accountId}/fields/{fieldId}/validate` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Fields | POST | `/v1/accounts/{accountId}/fields/validate-multiple` | application/json: object[] (required) | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Fields | GET | `/v1/field-types` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Users | GET | `/v1/users/self/notification-preferences` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Users | PUT | `/v1/users/self/notification-preferences` | application/json: NotificationPreferences (required) | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 500 |
| Documents | GET | `/v1/documents/{documentId}/thumbnail` | none | image/*: string | bearerAuth or apiKeyAuth | 401, 404, 500 |
| Documents | GET | `/v1/documents/{documentId}/pages/{pageId}/download` | none | image/*: string | bearerAuth or apiKeyAuth | 401, 404, 500 |
| Signing | GET | `/v1/public/documents/{documentId}` | none | application/json: object | public | 404, 500 |
| Signing | PUT | `/v1/public/documents/{documentId}/send-token` | application/json: object (optional) | application/json: Envelope | public | 500 |
| Signers | GET | `/v1/accounts/{accountId}/signers` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Signers | POST | `/v1/accounts/{accountId}/signers` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 500 |
| Signers | GET | `/v1/accounts/{accountId}/signers/{signerId}` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 404, 500 |
| Signers | PUT | `/v1/accounts/{accountId}/signers/{signerId}` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 404, 500 |
| Signers | DELETE | `/v1/accounts/{accountId}/signers/{signerId}` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 404, 500 |
| Signing | GET | `/v1/signers/self` | none | application/json: object | signerAccessCode | 401, 500 |
| Signing | GET | `/v1/signers/{signerId}/document` | none | application/json: object | signerAccessCode | 401, 404, 500 |
| Signing | GET | `/v1/sign` | none | application/json: object | signerAccessCode | 400, 401, 409, 500 |
| Signing | POST | `/v1/documents/{documentId}/assignments/{assignmentId}` | application/json: object[] (required) | application/json: object | signerAccessCode | 400, 401, 409, 500 |
| Signing | PUT | `/v1/documents/{documentId}/assignments/{assignmentId}/reject` | application/json: object (required) | application/json: object | signerAccessCode | 401, 500 |
| Signing | PUT | `/v1/signers/documents/sign-multiple` | application/json: object (required) | application/json: object | signerAccessCode | 401, 500 |
| Signing | PUT | `/v1/signers/documents/decline-multiple` | application/json: object (required) | application/json: object | signerAccessCode | 401, 500 |
| Signing | POST | `/v1/verify` | application/json: object (required) | application/json: Envelope | signerAccessCode | 400, 401, 500 |
| Signing | PUT | `/v1/documents/{documentId}/signers/confirm-data` | application/json: object (required) | application/json: object | signerAccessCode | 401, 500 |
| Signing | PUT | `/v1/signers/accept-terms` | none | application/json: Envelope | signerAccessCode | 401, 500 |
| Signing | POST | `/v1/signature` | image/png: string (required) | application/json: Envelope | signerAccessCode | 401, 500 |
| Signing | GET | `/v1/signature/{signatureType}` | none | image/*: string | signerAccessCode | 401, 404, 500 |
| Signing | GET | `/v1/signers/{signerId}/documents` | none | application/json: object | signerAccessCode | 401, 500 |
| Signing | GET | `/v1/signers/{signerId}/documents/search` | none | application/json: object | signerAccessCode | 401, 500 |
| Signing | GET | `/v1/signers/{signerId}/documents/{documentId}/download/{artifactName}` | none | application/pdf: string | public | 404, 500 |
| Authentication | POST | `/v1/authentication/social-login` | application/json: object (required) | application/json: object | public | 400, 500 |
| Authentication | POST | `/v1/auth/link-social-login` | application/json: object (required) | application/json: Envelope | bearerAuth or apiKeyAuth | 400, 401, 500 |
| Accounts | GET | `/v1/accounts/{accountId}/stats` | none | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 500 |
| Users | GET | `/v1/users/self/stats` | none | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 500 |
| Tags | GET | `/v1/accounts/{accountId}/tags` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Tags | POST | `/v1/accounts/{accountId}/tags` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 409, 500 |
| Tags | PUT | `/v1/accounts/{accountId}/tags/{tagId}` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 404, 500 |
| Tags | DELETE | `/v1/accounts/{accountId}/tags/{tagId}` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 404, 500 |
| Templates | GET | `/v1/accounts/{accountId}/templates` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Documents | POST | `/v1/accounts/{accountId}/templates/{templateId}/documents` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 500 |
| Documents | POST | `/v1/accounts/{accountId}/templates/{templateId}/documents/estimate-cost` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Users | GET | `/v1/users/self` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Authentication | GET | `/v1/users/api-keys` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Authentication | POST | `/v1/users/api-keys` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Authentication | DELETE | `/v1/users/api-keys` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Webhooks | GET | `/v1/accounts/{accountId}/webhooks/subscriptions` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Webhooks | PUT | `/v1/accounts/{accountId}/webhooks/subscriptions` | application/json: object (required) | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 500 |
| Webhooks | PUT | `/v1/accounts/{accountId}/webhooks/inactivate` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Webhooks | GET | `/v1/webhooks/event-types` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Webhooks | GET | `/v1/accounts/{accountId}/webhooks` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |
| Webhooks | POST | `/v1/accounts/{accountId}/webhooks/{historyId}/retry` | none | application/json: object | bearerAuth or apiKeyAuth | 400, 401, 404, 500 |
| Assignments | GET | `/v1/documents/{documentId}/assignments/{assignmentId}/whatsapp-notifications` | none | application/json: object | bearerAuth or apiKeyAuth | 401, 500 |

## Full operation request and response payloads

### GET /v1/accounts/{accountId}

Get account. Security: **bearerAuth or apiKeyAuth**.

Retrieve a workspace account the user belongs to.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |

Request body: none.

Success response:
The account

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Account"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "account",
    "id": "6401df46d6a6b0c692d9ec49",
    "name": "Acme Inc.",
    "primary_color": "aabbcc",
    "secondary_color": "112233",
    "notification_sender_type": "User",
    "roles": [
      "owner"
    ],
    "is_delete_allowed": true,
    "created_at": "2026-06-03T03:54:16Z"
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### PUT /v1/accounts/{accountId}

Update account. Security: **bearerAuth or apiKeyAuth**.

Update a workspace account's profile.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |

Request body: required.
`application/json` schema:

```json
{
  "properties": {
    "name": {
      "type": "string",
      "example": "Acme Inc."
    },
    "notification_sender_type": {
      "description": "Who signers see as the notification sender for documents in this account. `User` (default) shows the document owner's name; `Account` shows this account's name.",
      "type": "string",
      "enum": [
        "User",
        "Account"
      ],
      "example": "Account"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "name": "Acme Inc.",
  "notification_sender_type": "Account"
}
```

Success response:
The updated account

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Account"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "account",
    "id": "6401df46d6a6b0c692d9ec49",
    "name": "Acme Inc.",
    "primary_color": "aabbcc",
    "secondary_color": "112233",
    "notification_sender_type": "User",
    "roles": [
      "owner"
    ],
    "is_delete_allowed": true,
    "created_at": "2026-06-03T03:54:16Z"
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### DELETE /v1/accounts/{accountId}

Delete account. Security: **bearerAuth or apiKeyAuth**.

Delete a workspace account.

By default the request fails with `400` when the workspace has an active paid subscription — the `restrictions` array in the response lists each blocker by code so you can address them individually before retrying. Pass `force: true` to cancel any active paid subscription automatically and proceed with immediate deletion.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |

Request body: optional.
`application/json` schema:

```json
{
  "properties": {
    "force": {
      "description": "When `true`, cancels any active paid subscription on this workspace and proceeds with deletion immediately. Defaults to `false`.",
      "type": "boolean",
      "example": false
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "force": false
}
```

Success response:
Account deleted

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": [],
          "example": []
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": []
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/accounts/{accountId}/theme

Get account theme. Security: **bearerAuth or apiKeyAuth**.

Retrieve account theme information (branding name, colors, and logo URL).

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |

Request body: none.

Success response:
The theme

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/AccountTheme"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "account_name": "Account Name",
    "primary_color": "aabbcc",
    "secondary_color": "aabbcc",
    "logo": "https://api.assinafy.com.br/v1/accounts/1a/logo"
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/accounts/{accountId}/logo

Download account logo. Security: **bearerAuth or apiKeyAuth**.

Download the account logo image binary.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |

Request body: none.

Success response:
The logo image

`image/*` schema:

```json
{
  "type": "string",
  "format": "binary"
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/accounts/{accountId}/logo

Upload account logo. Security: **bearerAuth or apiKeyAuth**.

Upload or replace the account logo image.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |

Request body: required.
`multipart/form-data` schema:

```json
{
  "required": [
    "file"
  ],
  "properties": {
    "file": {
      "type": "string",
      "format": "binary"
    }
  },
  "type": "object"
}
```

Success response:
Logo updated

`application/json` schema:

```json
{
  "$ref": "#/components/schemas/Envelope"
}
```

Example payload:

```json
{
  "status": 200,
  "message": ""
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### DELETE /v1/accounts/{accountId}/logo

Delete account logo. Security: **bearerAuth or apiKeyAuth**.

Remove the account logo image.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |

Request body: none.

Success response:
Logo deleted

`application/json` schema:

```json
{
  "$ref": "#/components/schemas/Envelope"
}
```

Example payload:

```json
{
  "status": 200,
  "message": ""
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/accounts

List my accounts. Security: **bearerAuth or apiKeyAuth**.

List the workspace accounts the authenticated user belongs to.

Parameters: none.

Request body: none.

Success response:
The user's accounts

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/Account"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "resource": "account",
      "id": "6401df46d6a6b0c692d9ec49",
      "name": "Acme Inc.",
      "primary_color": "aabbcc",
      "secondary_color": "112233",
      "notification_sender_type": "User",
      "roles": [
        "owner"
      ],
      "is_delete_allowed": true,
      "created_at": "2026-06-03T03:54:16Z"
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/accounts

Create account. Security: **bearerAuth or apiKeyAuth**.

Create a new workspace account owned by the authenticated user.

Parameters: none.

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "name"
  ],
  "properties": {
    "name": {
      "type": "string",
      "example": "Acme Inc."
    },
    "notification_sender_type": {
      "description": "Who signers see as the notification sender for documents in this account. `User` (default) shows the document owner's name; `Account` shows this account's name.",
      "type": "string",
      "enum": [
        "User",
        "Account"
      ],
      "example": "Account"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "name": "Acme Inc.",
  "notification_sender_type": "Account"
}
```

Success response:
The created account

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Account"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "account",
    "id": "6401df46d6a6b0c692d9ec49",
    "name": "Acme Inc.",
    "primary_color": "aabbcc",
    "secondary_color": "112233",
    "notification_sender_type": "User",
    "roles": [
      "owner"
    ],
    "is_delete_allowed": true,
    "created_at": "2026-06-03T03:54:16Z"
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/documents/{documentId}/activities

List document activities. Security: **bearerAuth or apiKeyAuth**.

List the activities recorded for a document. Each entry carries an event-specific `payload` snapshot (keys vary per event) and the request `origin` (`ip`, `user-agent`).

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentId` | path | yes | `{"type":"string"}` | Document ID. |

Request body: none.

Success response:
Document activities

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/DocumentActivity"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "id": 4,
      "event": "assignment_created",
      "message": "Assignment created by John Smith.",
      "payload": {},
      "origin": {
        "ip": "172.19.0.1",
        "user-agent": "string"
      },
      "created_at": "2022-07-19T19:28:13Z"
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/assignments

List assignments. Security: **bearerAuth or apiKeyAuth**.

List the assignments belonging to the authenticated user's current account.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `page` | query | no | `{"type":"integer","minimum":1}` | Page number. |
| `per-page` | query | no | `{"type":"integer","maximum":100}` | Records per page (max 100). |

Request body: none.

Success response:
A page of assignments

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/Assignment"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "resource": "assignment",
      "id": "615606ef81d199996981dbce",
      "sender_email": "sender@example.com",
      "method": "virtual",
      "expires_at": "2026-08-19T12:00:00Z",
      "message": "string",
      "signers": [
        {
          "resource": "signer",
          "id": "62d6ee35c7741ca4006b9e11",
          "full_name": "John Signer",
          "email": "john@example.com",
          "whatsapp_phone_number": "+5548999990000",
          "has_accepted_terms": false,
          "verification_method": "Email",
          "notification_methods": [
            "Email"
          ],
          "step": 1,
          "notified": false,
          "completed": false,
          "notification_history": [
            {
              "event": "signature_request",
              "status": "sent",
              "error_code": "string",
              "error_message": "string",
              "sent_at": "2026-07-07T12:00:00Z",
              "failed_at": "2026-08-19T12:00:00Z"
            }
          ]
        }
      ],
      "copy_receivers": [
        {}
      ],
      "items": [
        {
          "id": "string",
          "page": {
            "id": "615601faf166d6d1d8e7dc30",
            "number": 1,
            "height": 2100,
            "width": 1275,
            "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
          },
          "signer": {},
          "field": {},
          "display_settings": {},
          "value": {},
          "completed": false
        }
      ],
      "summary": {
        "signer_count": 0,
        "completed_count": 0,
        "signers": [
          {}
        ]
      },
      "signing_urls": [
        {
          "signer_id": "string",
          "url": "https://api.assinafy.com.br/v1/sign/doc1?email=joe@example.com"
        }
      ]
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/documents/{documentId}/assignments

Create assignment (request signatures). Security: **bearerAuth or apiKeyAuth**.

Request signatures on a document. Use `method: virtual` to sign without input fields, or `method: collect` to place input fields on specific pages.

For **virtual**, the document may be in `uploaded`, `metadata_processing` or `metadata_ready`; it is promoted to `pending_signature` automatically once metadata processing completes. For **collect**, the document must be in `metadata_ready` (fields reference specific pages).

`step` controls signing order: signers sharing a step sign in parallel, and the next step is notified only after the previous step completes. If supplied, every signer must supply it and values must be contiguous starting at 1.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentId` | path | yes | `{"type":"string"}` | Document ID. |

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "method",
    "signers"
  ],
  "properties": {
    "method": {
      "type": "string",
      "enum": [
        "virtual",
        "collect"
      ],
      "example": "virtual"
    },
    "signers": {
      "type": "array",
      "items": {
        "required": [
          "id"
        ],
        "properties": {
          "id": {
            "type": "string",
            "example": "615605f50e968054a5b7c9b8"
          },
          "verification_method": {
            "description": "How the signer's identity is verified before signing. `Email` (default) sends a one-time code to the signer's email; `Whatsapp` sends the code over WhatsApp (incurs an additional cost and is available only on paid subscriptions); `DigitalCertificate` has the signer sign with their own ICP-Brasil certificate (A1/A3) — it requires the Digital Certificate feature, the signer must have a CPF or CNPJ in `government_id`, must be alone in its signing step, and is charged 2 credits per signer. A CPF requires that person's certificate (an e-CPF, or an e-CNPJ naming them as legal representative); a CNPJ requires an e-CNPJ for that company, from any of its representatives. Omit to default to `Email`.",
            "type": "string",
            "enum": [
              "Email",
              "Whatsapp",
              "DigitalCertificate"
            ],
            "example": "Email"
          },
          "notification_methods": {
            "description": "Channels used to notify the signer of the request. Any combination of `Email` and `Whatsapp` (WhatsApp incurs an additional cost and is available only on paid subscriptions). Omit to default to `{\"Email\"}`.",
            "type": "array",
            "items": {
              "type": "string",
              "enum": [
                "Email",
                "Whatsapp"
              ]
            },
            "example": [
              "Email"
            ]
          },
          "step": {
            "type": "integer",
            "example": 1
          }
        },
        "type": "object"
      }
    },
    "entries": {
      "description": "Required for `collect`: field placements per page.",
      "type": "array",
      "items": {
        "properties": {
          "page_id": {
            "type": "string"
          },
          "fields": {
            "type": "array",
            "items": {
              "properties": {
                "signer_id": {
                  "type": "string"
                },
                "field_id": {
                  "type": "string"
                },
                "display_settings": {
                  "$ref": "#/components/schemas/DisplaySettings"
                }
              },
              "type": "object"
            }
          }
        },
        "type": "object"
      }
    },
    "message": {
      "description": "Text included in the invitation email.",
      "type": "string"
    },
    "expires_at": {
      "description": "ISO 8601; default is no expiration.",
      "type": "string",
      "format": "date-time"
    },
    "copy_receivers": {
      "description": "Signer IDs that only receive a copy.",
      "type": "array",
      "items": {
        "type": "string"
      }
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "method": "virtual",
  "signers": [
    {
      "id": "615605f50e968054a5b7c9b8",
      "verification_method": "Email",
      "notification_methods": [
        "Email"
      ],
      "step": 1
    }
  ],
  "entries": [
    {
      "page_id": "string",
      "fields": [
        {
          "signer_id": "string",
          "field_id": "string",
          "display_settings": {
            "left": 69,
            "top": 282,
            "width": 421,
            "height": 45.86,
            "fontFamily": "Arial",
            "fontSize": 22,
            "backgroundColor": "#D5EBFF"
          }
        }
      ]
    }
  ],
  "message": "string",
  "expires_at": "2026-08-19T12:00:00Z",
  "copy_receivers": [
    "string"
  ]
}
```

Success response:
The created assignment

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Assignment"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "assignment",
    "id": "615606ef81d199996981dbce",
    "sender_email": "sender@example.com",
    "method": "virtual",
    "expires_at": "2026-08-19T12:00:00Z",
    "message": "string",
    "signers": [
      {
        "resource": "signer",
        "id": "62d6ee35c7741ca4006b9e11",
        "full_name": "John Signer",
        "email": "john@example.com",
        "whatsapp_phone_number": "+5548999990000",
        "has_accepted_terms": false,
        "verification_method": "Email",
        "notification_methods": [
          "Email"
        ],
        "step": 1,
        "notified": false,
        "completed": false,
        "notification_history": [
          {
            "event": "signature_request",
            "status": "sent",
            "error_code": "string",
            "error_message": "string",
            "sent_at": "2026-07-07T12:00:00Z",
            "failed_at": "2026-08-19T12:00:00Z"
          }
        ]
      }
    ],
    "copy_receivers": [
      {}
    ],
    "items": [
      {
        "id": "string",
        "page": {
          "id": "615601faf166d6d1d8e7dc30",
          "number": 1,
          "height": 2100,
          "width": 1275,
          "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
        },
        "signer": {},
        "field": {},
        "display_settings": {},
        "value": {},
        "completed": false
      }
    ],
    "summary": {
      "signer_count": 0,
      "completed_count": 0,
      "signers": [
        {}
      ]
    },
    "signing_urls": [
      {
        "signer_id": "string",
        "url": "https://api.assinafy.com.br/v1/sign/doc1?email=joe@example.com"
      }
    ]
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/documents/{documentId}/assignments/estimate-cost

Estimate assignment cost. Security: **bearerAuth or apiKeyAuth**.

Estimate the cost of creating an assignment without creating it, returning a cost breakdown and the current account balances. Signer IDs are not required — only the verification/notification method affects cost. Each assignment consumes 1 document from the plan allowance; if exhausted, an extra document is charged from credits (`needs_extra_document` = true). `blocking_reason` may be `PendingPayment`, `InsufficientDocuments` or `InsufficientCredits`.

### Pricing

Per-unit costs (in credits) used to build the estimate:

| Item | Cost |
|------|------|
| Extra document | 1 credit |
| Email notification | 0 credits |
| WhatsApp notification | 0.45 credits |
| Digital certificate signature (per signer) | 2 credits |

A `DigitalCertificate` signer adds the digital-certificate signature cost **on top of** its notification cost; it appears in the `breakdown` under the `SignatureDigitalCertificate` code.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentId` | path | yes | `{"type":"string"}` | Document ID. |

Request body: required.
`application/json` schema:

```json
{
  "properties": {
    "method": {
      "type": "string",
      "enum": [
        "virtual",
        "collect"
      ],
      "example": "virtual"
    },
    "signers": {
      "description": "Required for `virtual`; each entry may be `{}` to default to Email.",
      "type": "array",
      "items": {
        "properties": {
          "verification_method": {
            "description": "Verification method to price. `Whatsapp` may incur an additional cost; `DigitalCertificate` adds the per-signer signature cost.",
            "type": "string",
            "enum": [
              "Email",
              "Whatsapp",
              "DigitalCertificate"
            ],
            "example": "Whatsapp"
          },
          "notification_methods": {
            "description": "Notification channels to price. `Whatsapp` may incur an additional cost.",
            "type": "array",
            "items": {
              "type": "string",
              "enum": [
                "Email",
                "Whatsapp"
              ]
            }
          }
        },
        "type": "object"
      }
    },
    "entries": {
      "description": "Required for `collect`.",
      "type": "array",
      "items": {
        "type": "object"
      }
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "method": "virtual",
  "signers": [
    {
      "verification_method": "Whatsapp",
      "notification_methods": [
        "Email"
      ]
    }
  ],
  "entries": [
    {}
  ]
}
```

Success response:
Cost estimate and balances

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/CostEstimate"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "documents": 1,
    "credits": 0,
    "needs_extra_document": false,
    "extra_document_cost": 1,
    "total_credits": 0,
    "breakdown": [
      {
        "code": "NotificationWhatsapp",
        "name": "Whatsapp Notification",
        "cost": 0.9,
        "quantity": 2,
        "unit_cost": 0.45
      }
    ],
    "document_balance": 0,
    "credit_balance": 0,
    "has_sufficient_resources": false,
    "blocking_reason": "PendingPayment",
    "message": "string"
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### PUT /v1/documents/{documentId}/assignments/{assignmentId}/signers/{signerId}/resend

Resend signature request. Security: **bearerAuth or apiKeyAuth**.

Resend the signature-request notification to a specific signer of an assignment.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentId` | path | yes | `{"type":"string"}` | Document ID. |
| `assignmentId` | path | yes | `{"type":"string"}` | The assignment ID. |
| `signerId` | path | yes | `{"type":"string"}` | The signer ID. |

Request body: none.

Success response:
Resend result

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "properties": {
            "is_sent": {
              "type": "boolean"
            },
            "document_id": {
              "type": "string"
            },
            "signer_id": {
              "type": "string"
            }
          },
          "type": "object"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "is_sent": false,
    "document_id": "string",
    "signer_id": "string"
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/documents/{documentId}/assignments/{assignmentId}/signers/{signerId}/estimate-resend-cost

Estimate resend cost. Security: **bearerAuth or apiKeyAuth**.

Estimate the cost of resending the signature request to a signer.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentId` | path | yes | `{"type":"string"}` | Document ID. |
| `assignmentId` | path | yes | `{"type":"string"}` | The assignment ID. |
| `signerId` | path | yes | `{"type":"string"}` | The signer ID. |

Request body: none.

Success response:
Cost estimate

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/CostEstimate"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "documents": 1,
    "credits": 0,
    "needs_extra_document": false,
    "extra_document_cost": 1,
    "total_credits": 0,
    "breakdown": [
      {
        "code": "NotificationWhatsapp",
        "name": "Whatsapp Notification",
        "cost": 0.9,
        "quantity": 2,
        "unit_cost": 0.45
      }
    ],
    "document_balance": 0,
    "credit_balance": 0,
    "has_sufficient_resources": false,
    "blocking_reason": "PendingPayment",
    "message": "string"
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### PUT /v1/documents/{documentId}/assignments/{assignmentId}/reset-expiration

Reset assignment expiration. Security: **bearerAuth or apiKeyAuth**.

Set a new expiration date for an assignment.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentId` | path | yes | `{"type":"string"}` | Document ID. |
| `assignmentId` | path | yes | `{"type":"string"}` | The assignment ID. |

Request body: required.
`application/json` schema:

```json
{
  "properties": {
    "expires_at": {
      "description": "New expiration date (ISO 8601).",
      "type": "string",
      "format": "date-time",
      "example": "2026-12-31T23:59:59Z"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "expires_at": "2026-12-31T23:59:59Z"
}
```

Success response:
The updated assignment

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Assignment"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "assignment",
    "id": "615606ef81d199996981dbce",
    "sender_email": "sender@example.com",
    "method": "virtual",
    "expires_at": "2026-08-19T12:00:00Z",
    "message": "string",
    "signers": [
      {
        "resource": "signer",
        "id": "62d6ee35c7741ca4006b9e11",
        "full_name": "John Signer",
        "email": "john@example.com",
        "whatsapp_phone_number": "+5548999990000",
        "has_accepted_terms": false,
        "verification_method": "Email",
        "notification_methods": [
          "Email"
        ],
        "step": 1,
        "notified": false,
        "completed": false,
        "notification_history": [
          {
            "event": "signature_request",
            "status": "sent",
            "error_code": "string",
            "error_message": "string",
            "sent_at": "2026-07-07T12:00:00Z",
            "failed_at": "2026-08-19T12:00:00Z"
          }
        ]
      }
    ],
    "copy_receivers": [
      {}
    ],
    "items": [
      {
        "id": "string",
        "page": {
          "id": "615601faf166d6d1d8e7dc30",
          "number": 1,
          "height": 2100,
          "width": 1275,
          "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
        },
        "signer": {},
        "field": {},
        "display_settings": {},
        "value": {},
        "completed": false
      }
    ],
    "summary": {
      "signer_count": 0,
      "completed_count": 0,
      "signers": [
        {}
      ]
    },
    "signing_urls": [
      {
        "signer_id": "string",
        "url": "https://api.assinafy.com.br/v1/sign/doc1?email=joe@example.com"
      }
    ]
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/login

Login. Security: **public**.

Authenticate with email and password and receive a JWT access token.

Parameters: none.

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "email",
    "password"
  ],
  "properties": {
    "email": {
      "type": "string",
      "format": "email",
      "example": "user@example.com"
    },
    "password": {
      "type": "string",
      "format": "password",
      "example": "password"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "email": "user@example.com",
  "password": "password"
}
```

Success response:
Access token, user and accounts

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/AuthSession"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "access_token": "example-access-token",
    "user": {
      "id": "bgjazeo5r9v2lq7l36dx48np",
      "name": "John Smith",
      "email": "example@example.com",
      "telephone": "17989206641",
      "government_id": "15774136604",
      "is_email_verified": false,
      "has_accepted_terms": true,
      "created_at": "2023-03-03T11:51:34Z",
      "to_be_deleted_at": "2026-08-19T12:00:00Z"
    },
    "accounts": [
      {
        "id": "6401df46d6a6b0c692d9ec49",
        "name": "JS",
        "roles": [
          "owner"
        ],
        "is_delete_allowed": true,
        "created_at": "2023-03-03T11:51:34Z"
      }
    ]
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### PUT /v1/authentication/request-password-reset

Request password reset. Security: **public**.

Send the user an email with instructions to reset their password. Used when the password was forgotten or never set.

Parameters: none.

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "email"
  ],
  "properties": {
    "email": {
      "type": "string",
      "format": "email",
      "example": "user@example.com"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "email": "user@example.com"
}
```

Success response:
Reset email sent

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "properties": {
            "email": {
              "type": "string",
              "format": "email",
              "example": "user@example.com"
            }
          },
          "type": "object"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "email": "user@example.com"
  }
}
```

Error responses:

#### 500

Error response.

No response payload documented.

### PUT /v1/authentication/reset-password

Reset password. Security: **public**.

Reset the user's password using the token received by email.

Parameters: none.

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "email",
    "new_password"
  ],
  "properties": {
    "email": {
      "type": "string",
      "format": "email",
      "example": "user@example.com"
    },
    "token": {
      "description": "Token received by email.",
      "type": "string",
      "example": "example-reset-token"
    },
    "new_password": {
      "type": "string",
      "format": "password",
      "example": "N3w_p4ssw0rd"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "email": "user@example.com",
  "token": "example-reset-token",
  "new_password": "N3w_p4ssw0rd"
}
```

Success response:
Password reset

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "properties": {
            "email": {
              "type": "string",
              "format": "email",
              "example": "user@example.com"
            }
          },
          "type": "object"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "email": "user@example.com"
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### PUT /v1/authentication/change-password

Change password. Security: **bearerAuth or apiKeyAuth**.

Change the authenticated user's password.

Parameters: none.

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "email",
    "password",
    "new_password"
  ],
  "properties": {
    "email": {
      "type": "string",
      "format": "email",
      "example": "user@example.com"
    },
    "password": {
      "description": "The current password.",
      "type": "string",
      "format": "password",
      "example": "X3$_!456aTa"
    },
    "new_password": {
      "description": "The new password.",
      "type": "string",
      "format": "password",
      "example": "X3$_!456aT"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "email": "user@example.com",
  "password": "X3$_!456aTa",
  "new_password": "X3$_!456aT"
}
```

Success response:
Password changed

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "properties": {
            "email": {
              "type": "string",
              "format": "email",
              "example": "user@example.com"
            }
          },
          "type": "object"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "email": "user@example.com"
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/accounts/{accountId}/documents

List documents. Security: **bearerAuth or apiKeyAuth**.

List documents of the workspace.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `status` | query | no | `{"type":"string"}` | Status filter, e.g. `pending_signature`. |
| `method` | query | no | `{"type":"string","enum":["virtual","collect"]}` | Signature method filter. |
| `search` | query | no | `{"type":"string"}` | Partial match on document.name, signer.full_name, signer.email. |
| `tags` | query | no | `{"type":"string"}` | Comma-separated tag IDs; returns documents having ALL listed tags. |
| `sort` | query | no | `{"type":"string"}` | Sort by `name` or `updated_at`. |
| `page` | query | no | `{"type":"integer","minimum":1}` | Page number. |
| `per-page` | query | no | `{"type":"integer","maximum":100}` | Records per page (max 100). |

Request body: none.

Success response:
A page of documents

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/Document"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "resource": "document",
      "id": "615601fab04c0a3147bb1246",
      "account_id": "d199996981dbd199996981db",
      "template_id": "string",
      "name": "document.pdf",
      "status": "metadata_ready",
      "artifacts": {
        "original": "https://api.assinafy.com.br/v1/documents/doc1/download/original"
      },
      "is_closed": false,
      "signing_url": "https://api.assinafy.com.br/v1/sign/doc1",
      "decline_reason": "string",
      "declined_by": {
        "resource": "signer",
        "id": "62d6ee35c7741ca4006b9e11",
        "full_name": "John Signer",
        "email": "john@example.com",
        "whatsapp_phone_number": "+5548999990000",
        "has_accepted_terms": false
      },
      "tags": [
        {
          "id": "string",
          "name": "string"
        }
      ],
      "assignment": {
        "resource": "assignment",
        "id": "615606ef81d199996981dbce",
        "sender_email": "sender@example.com",
        "method": "virtual",
        "expires_at": "2026-08-19T12:00:00Z",
        "message": "string",
        "signers": [
          {
            "resource": "signer",
            "id": "62d6ee35c7741ca4006b9e11",
            "full_name": "John Signer",
            "email": "john@example.com",
            "whatsapp_phone_number": "+5548999990000",
            "has_accepted_terms": false,
            "verification_method": "Email",
            "notification_methods": [
              "Email"
            ],
            "step": 1,
            "notified": false,
            "completed": false,
            "notification_history": [
              {
                "event": "signature_request",
                "status": "sent",
                "error_code": "string",
                "error_message": "string",
                "sent_at": "2026-07-07T12:00:00Z",
                "failed_at": "2026-08-19T12:00:00Z"
              }
            ]
          }
        ],
        "copy_receivers": [
          {}
        ],
        "items": [
          {
            "id": "string",
            "page": {
              "id": "615601faf166d6d1d8e7dc30",
              "number": 1,
              "height": 2100,
              "width": 1275,
              "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
            },
            "signer": {},
            "field": {},
            "display_settings": {},
            "value": {},
            "completed": false
          }
        ],
        "summary": {
          "signer_count": 0,
          "completed_count": 0,
          "signers": [
            {}
          ]
        },
        "signing_urls": [
          {
            "signer_id": "string",
            "url": "https://api.assinafy.com.br/v1/sign/doc1?email=joe@example.com"
          }
        ]
      },
      "pages": [
        {
          "id": "615601faf166d6d1d8e7dc30",
          "number": 1,
          "height": 2100,
          "width": 1275,
          "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
        }
      ],
      "created_at": "2026-06-03T03:54:16Z",
      "updated_at": "2026-06-03T03:54:16Z"
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/accounts/{accountId}/documents

Upload and create document. Security: **bearerAuth or apiKeyAuth**.

Create a document from an uploaded file. Maximum file size 25MB; maximum 2000 pages.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |

Request body: required.
`multipart/form-data` schema:

```json
{
  "required": [
    "file"
  ],
  "properties": {
    "file": {
      "description": "The PDF file to upload.",
      "type": "string",
      "format": "binary"
    }
  },
  "type": "object"
}
```

Success response:
The created document

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Document"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "document",
    "id": "615601fab04c0a3147bb1246",
    "account_id": "d199996981dbd199996981db",
    "template_id": "string",
    "name": "document.pdf",
    "status": "metadata_ready",
    "artifacts": {
      "original": "https://api.assinafy.com.br/v1/documents/doc1/download/original"
    },
    "is_closed": false,
    "signing_url": "https://api.assinafy.com.br/v1/sign/doc1",
    "decline_reason": "string",
    "declined_by": {
      "resource": "signer",
      "id": "62d6ee35c7741ca4006b9e11",
      "full_name": "John Signer",
      "email": "john@example.com",
      "whatsapp_phone_number": "+5548999990000",
      "has_accepted_terms": false
    },
    "tags": [
      {
        "id": "string",
        "name": "string"
      }
    ],
    "assignment": {
      "resource": "assignment",
      "id": "615606ef81d199996981dbce",
      "sender_email": "sender@example.com",
      "method": "virtual",
      "expires_at": "2026-08-19T12:00:00Z",
      "message": "string",
      "signers": [
        {
          "resource": "signer",
          "id": "62d6ee35c7741ca4006b9e11",
          "full_name": "John Signer",
          "email": "john@example.com",
          "whatsapp_phone_number": "+5548999990000",
          "has_accepted_terms": false,
          "verification_method": "Email",
          "notification_methods": [
            "Email"
          ],
          "step": 1,
          "notified": false,
          "completed": false,
          "notification_history": [
            {
              "event": "signature_request",
              "status": "sent",
              "error_code": "string",
              "error_message": "string",
              "sent_at": "2026-07-07T12:00:00Z",
              "failed_at": "2026-08-19T12:00:00Z"
            }
          ]
        }
      ],
      "copy_receivers": [
        {}
      ],
      "items": [
        {
          "id": "string",
          "page": {
            "id": "615601faf166d6d1d8e7dc30",
            "number": 1,
            "height": 2100,
            "width": 1275,
            "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
          },
          "signer": {},
          "field": {},
          "display_settings": {},
          "value": {},
          "completed": false
        }
      ],
      "summary": {
        "signer_count": 0,
        "completed_count": 0,
        "signers": [
          {}
        ]
      },
      "signing_urls": [
        {
          "signer_id": "string",
          "url": "https://api.assinafy.com.br/v1/sign/doc1?email=joe@example.com"
        }
      ]
    },
    "pages": [
      {
        "id": "615601faf166d6d1d8e7dc30",
        "number": 1,
        "height": 2100,
        "width": 1275,
        "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
      }
    ],
    "created_at": "2026-06-03T03:54:16Z",
    "updated_at": "2026-06-03T03:54:16Z"
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/accounts/{accountId}/documents/search

Search documents (lightweight). Security: **bearerAuth or apiKeyAuth**.

Search documents of the workspace, returning a compact representation (no expanded assignment/pages).

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `search` | query | no | `{"type":"string"}` | Search term. |
| `status` | query | no | `{"type":"string"}` |  |
| `page` | query | no | `{"type":"integer","minimum":1}` | Page number. |
| `per-page` | query | no | `{"type":"integer","maximum":100}` | Records per page (max 100). |

Request body: none.

Success response:
Matching documents

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/Document"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "resource": "document",
      "id": "615601fab04c0a3147bb1246",
      "account_id": "d199996981dbd199996981db",
      "template_id": "string",
      "name": "document.pdf",
      "status": "metadata_ready",
      "artifacts": {
        "original": "https://api.assinafy.com.br/v1/documents/doc1/download/original"
      },
      "is_closed": false,
      "signing_url": "https://api.assinafy.com.br/v1/sign/doc1",
      "decline_reason": "string",
      "declined_by": {
        "resource": "signer",
        "id": "62d6ee35c7741ca4006b9e11",
        "full_name": "John Signer",
        "email": "john@example.com",
        "whatsapp_phone_number": "+5548999990000",
        "has_accepted_terms": false
      },
      "tags": [
        {
          "id": "string",
          "name": "string"
        }
      ],
      "assignment": {
        "resource": "assignment",
        "id": "615606ef81d199996981dbce",
        "sender_email": "sender@example.com",
        "method": "virtual",
        "expires_at": "2026-08-19T12:00:00Z",
        "message": "string",
        "signers": [
          {
            "resource": "signer",
            "id": "62d6ee35c7741ca4006b9e11",
            "full_name": "John Signer",
            "email": "john@example.com",
            "whatsapp_phone_number": "+5548999990000",
            "has_accepted_terms": false,
            "verification_method": "Email",
            "notification_methods": [
              "Email"
            ],
            "step": 1,
            "notified": false,
            "completed": false,
            "notification_history": [
              {
                "event": "signature_request",
                "status": "sent",
                "error_code": "string",
                "error_message": "string",
                "sent_at": "2026-07-07T12:00:00Z",
                "failed_at": "2026-08-19T12:00:00Z"
              }
            ]
          }
        ],
        "copy_receivers": [
          {}
        ],
        "items": [
          {
            "id": "string",
            "page": {
              "id": "615601faf166d6d1d8e7dc30",
              "number": 1,
              "height": 2100,
              "width": 1275,
              "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
            },
            "signer": {},
            "field": {},
            "display_settings": {},
            "value": {},
            "completed": false
          }
        ],
        "summary": {
          "signer_count": 0,
          "completed_count": 0,
          "signers": [
            {}
          ]
        },
        "signing_urls": [
          {
            "signer_id": "string",
            "url": "https://api.assinafy.com.br/v1/sign/doc1?email=joe@example.com"
          }
        ]
      },
      "pages": [
        {
          "id": "615601faf166d6d1d8e7dc30",
          "number": 1,
          "height": 2100,
          "width": 1275,
          "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
        }
      ],
      "created_at": "2026-06-03T03:54:16Z",
      "updated_at": "2026-06-03T03:54:16Z"
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/documents/statuses

List document statuses. Security: **bearerAuth or apiKeyAuth**.

The supported document statuses and whether a document in each status can be deleted.

| Status | Deletable | Description |
|--------|-----------|-------------|
| `uploading` | no | The document upload is in process. |
| `uploaded` | no | The document has been uploaded. |
| `metadata_processing` | no | The initial processing is under way. |
| `metadata_ready` | yes | The initial processing has been completed. |
| `expired` | yes | The signature deadline has been reached. |
| `certificating` | no | The document has been signed and is being certificated. |
| `certificated` | no | The document is certificated. |
| `rejected_by_signer` | yes | A signer declined signing the document. |
| `pending_signature` | yes | The document is waiting for signatures. |
| `rejected_by_user` | yes | The signature process was cancelled by a user. |
| `failed` | yes | The document processing has failed. |

Parameters: none.

Request body: none.

Success response:
Supported statuses

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/DocumentStatus"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "code": "metadata_ready",
      "deletable": true
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/documents/{documentId}

Get document. Security: **bearerAuth or apiKeyAuth**.

Get a document by its ID. `decline_reason` is only present when the access token belongs to the document's creator.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentId` | path | yes | `{"type":"string"}` | Document ID. |

Request body: none.

Success response:
The document

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Document"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "document",
    "id": "615601fab04c0a3147bb1246",
    "account_id": "d199996981dbd199996981db",
    "template_id": "string",
    "name": "document.pdf",
    "status": "metadata_ready",
    "artifacts": {
      "original": "https://api.assinafy.com.br/v1/documents/doc1/download/original"
    },
    "is_closed": false,
    "signing_url": "https://api.assinafy.com.br/v1/sign/doc1",
    "decline_reason": "string",
    "declined_by": {
      "resource": "signer",
      "id": "62d6ee35c7741ca4006b9e11",
      "full_name": "John Signer",
      "email": "john@example.com",
      "whatsapp_phone_number": "+5548999990000",
      "has_accepted_terms": false
    },
    "tags": [
      {
        "id": "string",
        "name": "string"
      }
    ],
    "assignment": {
      "resource": "assignment",
      "id": "615606ef81d199996981dbce",
      "sender_email": "sender@example.com",
      "method": "virtual",
      "expires_at": "2026-08-19T12:00:00Z",
      "message": "string",
      "signers": [
        {
          "resource": "signer",
          "id": "62d6ee35c7741ca4006b9e11",
          "full_name": "John Signer",
          "email": "john@example.com",
          "whatsapp_phone_number": "+5548999990000",
          "has_accepted_terms": false,
          "verification_method": "Email",
          "notification_methods": [
            "Email"
          ],
          "step": 1,
          "notified": false,
          "completed": false,
          "notification_history": [
            {
              "event": "signature_request",
              "status": "sent",
              "error_code": "string",
              "error_message": "string",
              "sent_at": "2026-07-07T12:00:00Z",
              "failed_at": "2026-08-19T12:00:00Z"
            }
          ]
        }
      ],
      "copy_receivers": [
        {}
      ],
      "items": [
        {
          "id": "string",
          "page": {
            "id": "615601faf166d6d1d8e7dc30",
            "number": 1,
            "height": 2100,
            "width": 1275,
            "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
          },
          "signer": {},
          "field": {},
          "display_settings": {},
          "value": {},
          "completed": false
        }
      ],
      "summary": {
        "signer_count": 0,
        "completed_count": 0,
        "signers": [
          {}
        ]
      },
      "signing_urls": [
        {
          "signer_id": "string",
          "url": "https://api.assinafy.com.br/v1/sign/doc1?email=joe@example.com"
        }
      ]
    },
    "pages": [
      {
        "id": "615601faf166d6d1d8e7dc30",
        "number": 1,
        "height": 2100,
        "width": 1275,
        "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
      }
    ],
    "created_at": "2026-06-03T03:54:16Z",
    "updated_at": "2026-06-03T03:54:16Z"
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### DELETE /v1/documents/{documentId}

Delete document. Security: **bearerAuth or apiKeyAuth**.

Delete a document by its ID. Only documents in a deletable status can be removed (see GET /v1/documents/statuses).

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentId` | path | yes | `{"type":"string"}` | Document ID. |

Request body: none.

Success response:
Document deleted

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": [],
          "example": []
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": []
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### PATCH /v1/documents/{documentId}

Rename document. Security: **bearerAuth or apiKeyAuth**.

Update a document's name. Only allowed before any assignment is created (i.e. while the document is in `uploaded` or `metadata_ready` status and has no signers yet); once the signature process has started or the document is certificated, the name is locked. The name is normalized: diacritics are removed and unsupported characters are replaced with dashes.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentId` | path | yes | `{"type":"string"}` | Document ID. |

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "name"
  ],
  "properties": {
    "name": {
      "type": "string",
      "maxLength": 255,
      "example": "Service agreement.pdf"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "name": "Service agreement.pdf"
}
```

Success response:
The updated document

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Document"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "document",
    "id": "615601fab04c0a3147bb1246",
    "account_id": "d199996981dbd199996981db",
    "template_id": "string",
    "name": "document.pdf",
    "status": "metadata_ready",
    "artifacts": {
      "original": "https://api.assinafy.com.br/v1/documents/doc1/download/original"
    },
    "is_closed": false,
    "signing_url": "https://api.assinafy.com.br/v1/sign/doc1",
    "decline_reason": "string",
    "declined_by": {
      "resource": "signer",
      "id": "62d6ee35c7741ca4006b9e11",
      "full_name": "John Signer",
      "email": "john@example.com",
      "whatsapp_phone_number": "+5548999990000",
      "has_accepted_terms": false
    },
    "tags": [
      {
        "id": "string",
        "name": "string"
      }
    ],
    "assignment": {
      "resource": "assignment",
      "id": "615606ef81d199996981dbce",
      "sender_email": "sender@example.com",
      "method": "virtual",
      "expires_at": "2026-08-19T12:00:00Z",
      "message": "string",
      "signers": [
        {
          "resource": "signer",
          "id": "62d6ee35c7741ca4006b9e11",
          "full_name": "John Signer",
          "email": "john@example.com",
          "whatsapp_phone_number": "+5548999990000",
          "has_accepted_terms": false,
          "verification_method": "Email",
          "notification_methods": [
            "Email"
          ],
          "step": 1,
          "notified": false,
          "completed": false,
          "notification_history": [
            {
              "event": "signature_request",
              "status": "sent",
              "error_code": "string",
              "error_message": "string",
              "sent_at": "2026-07-07T12:00:00Z",
              "failed_at": "2026-08-19T12:00:00Z"
            }
          ]
        }
      ],
      "copy_receivers": [
        {}
      ],
      "items": [
        {
          "id": "string",
          "page": {
            "id": "615601faf166d6d1d8e7dc30",
            "number": 1,
            "height": 2100,
            "width": 1275,
            "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
          },
          "signer": {},
          "field": {},
          "display_settings": {},
          "value": {},
          "completed": false
        }
      ],
      "summary": {
        "signer_count": 0,
        "completed_count": 0,
        "signers": [
          {}
        ]
      },
      "signing_urls": [
        {
          "signer_id": "string",
          "url": "https://api.assinafy.com.br/v1/sign/doc1?email=joe@example.com"
        }
      ]
    },
    "pages": [
      {
        "id": "615601faf166d6d1d8e7dc30",
        "number": 1,
        "height": 2100,
        "width": 1275,
        "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
      }
    ],
    "created_at": "2026-06-03T03:54:16Z",
    "updated_at": "2026-06-03T03:54:16Z"
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/documents/{documentId}/download/{artifactName}

Download document artifact. Security: **bearerAuth or apiKeyAuth**.

Download a document artifact. Artifact types: original, certificated, certificate-page, pades, bundle. The pades artifact (signers' ICP-Brasil signatures + platform certification box) is only present on documents that had digital-certificate signers; `bundle` is a zip of the original, certificated and certificate-page artifacts, plus the pades artifact on documents that have one.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentId` | path | yes | `{"type":"string"}` | Document ID. |
| `artifactName` | path | yes | `{"type":"string","enum":["original","certificated","certificate-page","pades","bundle"]}` | Artifact type. |

Request body: none.

Success response:
The artifact binary

`application/pdf` schema:

```json
{
  "type": "string",
  "format": "binary"
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/documents/{documentSignatureHash}/verify

Verify a signed document. Security: **public**.

Verify a document by its signature hash (found on a signed document) and return its certification details. Always returns `200`: when the hash is not found or the document is not signed, `is_valid` is `false`, the other fields are `null`, and `message` explains why. Public endpoint.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentSignatureHash` | path | yes | `{"type":"string"}` | The document signature hash. |

Request body: none.

Success response:
Verification result

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/DocumentVerification"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "hash": "FE32EDDADE7CBDDCBB934E7402047450B0E59C02",
    "id": "63ddb172402799bfc991d10d",
    "status": "certificated",
    "page_count": "1",
    "signer_count": "1",
    "completed_count": 1,
    "completed_at": "2023-01-27T19:27:44Z",
    "verified_at": "2023-01-27T19:27:46Z",
    "is_valid": true,
    "message": ""
  }
}
```

Error responses:

#### 500

Error response.

No response payload documented.

### GET /v1/accounts/{accountId}/documents/{documentId}/tags

List document tags. Security: **bearerAuth or apiKeyAuth**.

List the tags attached to a document.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `documentId` | path | yes | `{"type":"string"}` | The document ID. |

Request body: none.

Success response:
Attached tags

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/Tag"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "resource": "tag",
      "id": "fa8c09f3e709a8a1c82d69b1454",
      "name": "Contracts",
      "color": "ff8800",
      "created_at": "2026-05-14T12:00:00Z",
      "updated_at": "2026-05-14T12:00:00Z"
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### PUT /v1/accounts/{accountId}/documents/{documentId}/tags

Replace document tags. Security: **bearerAuth or apiKeyAuth**.

Replace the full set of tags attached to a document.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `documentId` | path | yes | `{"type":"string"}` | The document ID. |

Request body: required.
`application/json` schema:

```json
{
  "properties": {
    "tags": {
      "description": "Tag IDs.",
      "type": "array",
      "items": {
        "type": "string"
      }
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "tags": [
    "string"
  ]
}
```

Success response:
Updated tags

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/Tag"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "resource": "tag",
      "id": "fa8c09f3e709a8a1c82d69b1454",
      "name": "Contracts",
      "color": "ff8800",
      "created_at": "2026-05-14T12:00:00Z",
      "updated_at": "2026-05-14T12:00:00Z"
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/accounts/{accountId}/documents/{documentId}/tags

Attach document tags. Security: **bearerAuth or apiKeyAuth**.

Attach one or more tags to a document.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `documentId` | path | yes | `{"type":"string"}` | The document ID. |

Request body: required.
`application/json` schema:

```json
{
  "properties": {
    "tags": {
      "description": "Tag IDs.",
      "type": "array",
      "items": {
        "type": "string"
      }
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "tags": [
    "string"
  ]
}
```

Success response:
Attached tags

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/Tag"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "resource": "tag",
      "id": "fa8c09f3e709a8a1c82d69b1454",
      "name": "Contracts",
      "color": "ff8800",
      "created_at": "2026-05-14T12:00:00Z",
      "updated_at": "2026-05-14T12:00:00Z"
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### DELETE /v1/accounts/{accountId}/documents/{documentId}/tags/{tagId}

Detach document tag. Security: **bearerAuth or apiKeyAuth**.

Detach a single tag from a document.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `documentId` | path | yes | `{"type":"string"}` | The document ID. |
| `tagId` | path | yes | `{"type":"string"}` | The tag ID. |

Request body: none.

Success response:
Tag detached

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "properties": {
            "detached": {
              "type": "boolean",
              "example": true
            }
          },
          "type": "object"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "detached": true
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/accounts/{accountId}/fields

List fields. Security: **bearerAuth or apiKeyAuth**.

List the field definitions of a workspace.

When `include_standard` is enabled, records of type `signature`, `initial` and `signatureDate` are also returned.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `include_inactive` | query | no | `{"type":"boolean"}` | Include inactive field definitions. |
| `include_standard` | query | no | `{"type":"boolean"}` | Include standard field types (signature, initial, signatureDate). |

Request body: none.

Success response:
Field definitions

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/Field"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "resource": "field",
      "id": "6152120297080d55bdd13197",
      "name": "Signature",
      "type": "signature",
      "regex": "string",
      "is_pre_defined": false,
      "is_active": false,
      "is_required": false,
      "is_standard": false,
      "is_read_only": false,
      "is_visible": false
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/accounts/{accountId}/fields

Create field. Security: **bearerAuth or apiKeyAuth**.

Create a field definition in the workspace.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "name",
    "type"
  ],
  "properties": {
    "name": {
      "type": "string",
      "example": "Full name"
    },
    "type": {
      "type": "string",
      "example": "text"
    },
    "regex": {
      "type": "string",
      "nullable": true
    },
    "is_required": {
      "type": "boolean"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "name": "Full name",
  "type": "text",
  "regex": "string",
  "is_required": false
}
```

Success response:
The created field

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Field"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "field",
    "id": "6152120297080d55bdd13197",
    "name": "Signature",
    "type": "signature",
    "regex": "string",
    "is_pre_defined": false,
    "is_active": false,
    "is_required": false,
    "is_standard": false,
    "is_read_only": false,
    "is_visible": false
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/accounts/{accountId}/fields/{fieldId}

Get field. Security: **bearerAuth or apiKeyAuth**.

Retrieve a single field definition.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `fieldId` | path | yes | `{"type":"string"}` | The field ID. |

Request body: none.

Success response:
The field

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Field"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "field",
    "id": "6152120297080d55bdd13197",
    "name": "Signature",
    "type": "signature",
    "regex": "string",
    "is_pre_defined": false,
    "is_active": false,
    "is_required": false,
    "is_standard": false,
    "is_read_only": false,
    "is_visible": false
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### PUT /v1/accounts/{accountId}/fields/{fieldId}

Update field. Security: **bearerAuth or apiKeyAuth**.

Update a field definition.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `fieldId` | path | yes | `{"type":"string"}` | The field ID. |

Request body: required.
`application/json` schema:

```json
{
  "properties": {
    "name": {
      "type": "string"
    },
    "regex": {
      "type": "string",
      "nullable": true
    },
    "is_active": {
      "type": "boolean"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "name": "string",
  "regex": "string",
  "is_active": false
}
```

Success response:
The updated field

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Field"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "field",
    "id": "6152120297080d55bdd13197",
    "name": "Signature",
    "type": "signature",
    "regex": "string",
    "is_pre_defined": false,
    "is_active": false,
    "is_required": false,
    "is_standard": false,
    "is_read_only": false,
    "is_visible": false
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### DELETE /v1/accounts/{accountId}/fields/{fieldId}

Delete field. Security: **bearerAuth or apiKeyAuth**.

Delete a field definition.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `fieldId` | path | yes | `{"type":"string"}` | The field ID. |

Request body: none.

Success response:
Field deleted

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": [],
          "example": []
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": []
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/accounts/{accountId}/fields/{fieldId}/validate

Validate field value. Security: **bearerAuth or apiKeyAuth**.

Validate an input value against a field definition. Typically called with a signer access code during the signing flow.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `fieldId` | path | yes | `{"type":"string"}` | The field ID. |

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "value"
  ],
  "properties": {
    "value": {
      "description": "The input value to validate.",
      "example": "400.676.228-36"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "value": "400.676.228-36"
}
```

Success response:
Validation result

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/FieldValidation"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "type": "cpf",
    "success": true,
    "error_message": ""
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/accounts/{accountId}/fields/validate-multiple

Validate multiple field values. Security: **bearerAuth or apiKeyAuth**.

Validate multiple input values at once. The request body is a JSON array of `{field_id, value}` objects.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |

Request body: required.
`application/json` schema:

```json
{
  "type": "array",
  "items": {
    "required": [
      "field_id",
      "value"
    ],
    "properties": {
      "field_id": {
        "description": "The field definition ID.",
        "type": "string",
        "example": "63488ffb7adf435aba319787"
      },
      "value": {
        "description": "The input value to validate.",
        "example": "1111111111111"
      }
    },
    "type": "object"
  }
}
```

Example payload:

```json
[
  {
    "field_id": "63488ffb7adf435aba319787",
    "value": "1111111111111"
  }
]
```

Success response:
Validation results

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/FieldValidationResult"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "field_id": "63488ffb7adf435aba319787",
      "type": "cpf",
      "success": false,
      "error_message": "Invalid CPF."
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/field-types

List field types. Security: **bearerAuth or apiKeyAuth**.

List the possible field types. `cpf` expects 11 digits; `cnpj` accepts 14-char values (letters A-Z allowed in positions 1–12 per the CNPJ Alfanumérico rule; check digits 13–14 stay numeric). Punctuation is ignored during validation.

Parameters: none.

Request body: none.

Success response:
Field types

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/FieldType"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "type": "cpf",
      "name": "CPF"
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/users/self/notification-preferences

Get my notification preferences. Security: **bearerAuth or apiKeyAuth**.

Which owner-facing document notifications the authenticated user receives by e-mail. All nine keys are always returned; everything defaults to `true`. Account and security e-mail (welcome, password reset, invitations, account deletion) is not configurable and never appears here.

Parameters: none.

Request body: none.

Success response:
The current preferences

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/NotificationPreferences"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "DocumentCompleted": true,
    "SignerDeclined": true,
    "DocumentCancelled": true,
    "DocumentAboutToExpire": true,
    "DocumentExpired": true,
    "DocumentExpirationReset": true,
    "DocumentProcessingFailed": true,
    "TemplateProcessingFailed": true,
    "SignerWhatsappFailed": true
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### PUT /v1/users/self/notification-preferences

Update my notification preferences. Security: **bearerAuth or apiKeyAuth**.

Merges the supplied map into the authenticated user's preferences. Send only the keys you want to change — omitted keys keep their current value. Setting a key to `false` stops that e-mail for this user in every account they belong to. Returns the full map. An unknown code, a non-boolean value, or an empty body is rejected with 400 and nothing is written.

Parameters: none.

Request body: required.
`application/json` schema:

```json
{
  "$ref": "#/components/schemas/NotificationPreferences"
}
```

Example payload:

```json
{
  "DocumentCompleted": true,
  "SignerDeclined": true,
  "DocumentCancelled": true,
  "DocumentAboutToExpire": true,
  "DocumentExpired": true,
  "DocumentExpirationReset": true,
  "DocumentProcessingFailed": true,
  "TemplateProcessingFailed": true,
  "SignerWhatsappFailed": true
}
```

Success response:
The updated preferences

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/NotificationPreferences"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "DocumentCompleted": true,
    "SignerDeclined": true,
    "DocumentCancelled": true,
    "DocumentAboutToExpire": true,
    "DocumentExpired": true,
    "DocumentExpirationReset": true,
    "DocumentProcessingFailed": true,
    "TemplateProcessingFailed": true,
    "SignerWhatsappFailed": true
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/documents/{documentId}/thumbnail

Download document thumbnail. Security: **bearerAuth or apiKeyAuth**.

Download the thumbnail image of a document's first page.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentId` | path | yes | `{"type":"string"}` | Document ID. |

Request body: none.

Success response:
The thumbnail image

`image/*` schema:

```json
{
  "type": "string",
  "format": "binary"
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/documents/{documentId}/pages/{pageId}/download

Download document page. Security: **bearerAuth or apiKeyAuth**.

Download the rendered image of a specific document page.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentId` | path | yes | `{"type":"string"}` | Document ID. |
| `pageId` | path | yes | `{"type":"string"}` | The page ID. |

Request body: none.

Success response:
The page image

`image/*` schema:

```json
{
  "type": "string",
  "format": "binary"
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/public/documents/{documentId}

View public document. Security: **public**.

Retrieve a publicly shared document by ID. Public endpoint.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentId` | path | yes | `{"type":"string"}` | The document ID. |

Request body: none.

Success response:
The public document

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Document"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "document",
    "id": "615601fab04c0a3147bb1246",
    "account_id": "d199996981dbd199996981db",
    "template_id": "string",
    "name": "document.pdf",
    "status": "metadata_ready",
    "artifacts": {
      "original": "https://api.assinafy.com.br/v1/documents/doc1/download/original"
    },
    "is_closed": false,
    "signing_url": "https://api.assinafy.com.br/v1/sign/doc1",
    "decline_reason": "string",
    "declined_by": {
      "resource": "signer",
      "id": "62d6ee35c7741ca4006b9e11",
      "full_name": "John Signer",
      "email": "john@example.com",
      "whatsapp_phone_number": "+5548999990000",
      "has_accepted_terms": false
    },
    "tags": [
      {
        "id": "string",
        "name": "string"
      }
    ],
    "assignment": {
      "resource": "assignment",
      "id": "615606ef81d199996981dbce",
      "sender_email": "sender@example.com",
      "method": "virtual",
      "expires_at": "2026-08-19T12:00:00Z",
      "message": "string",
      "signers": [
        {
          "resource": "signer",
          "id": "62d6ee35c7741ca4006b9e11",
          "full_name": "John Signer",
          "email": "john@example.com",
          "whatsapp_phone_number": "+5548999990000",
          "has_accepted_terms": false,
          "verification_method": "Email",
          "notification_methods": [
            "Email"
          ],
          "step": 1,
          "notified": false,
          "completed": false,
          "notification_history": [
            {
              "event": "signature_request",
              "status": "sent",
              "error_code": "string",
              "error_message": "string",
              "sent_at": "2026-07-07T12:00:00Z",
              "failed_at": "2026-08-19T12:00:00Z"
            }
          ]
        }
      ],
      "copy_receivers": [
        {}
      ],
      "items": [
        {
          "id": "string",
          "page": {
            "id": "615601faf166d6d1d8e7dc30",
            "number": 1,
            "height": 2100,
            "width": 1275,
            "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
          },
          "signer": {},
          "field": {},
          "display_settings": {},
          "value": {},
          "completed": false
        }
      ],
      "summary": {
        "signer_count": 0,
        "completed_count": 0,
        "signers": [
          {}
        ]
      },
      "signing_urls": [
        {
          "signer_id": "string",
          "url": "https://api.assinafy.com.br/v1/sign/doc1?email=joe@example.com"
        }
      ]
    },
    "pages": [
      {
        "id": "615601faf166d6d1d8e7dc30",
        "number": 1,
        "height": 2100,
        "width": 1275,
        "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
      }
    ],
    "created_at": "2026-06-03T03:54:16Z",
    "updated_at": "2026-06-03T03:54:16Z"
  }
}
```

Error responses:

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### PUT /v1/public/documents/{documentId}/send-token

Send access token for public document. Security: **public**.

Send a one-time access token (email/WhatsApp) to view a public document. Public endpoint.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentId` | path | yes | `{"type":"string"}` | The document ID. |

Request body: optional.
`application/json` schema:

```json
{
  "properties": {
    "email": {
      "type": "string",
      "format": "email"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "email": "user@example.com"
}
```

Success response:
Token sent

`application/json` schema:

```json
{
  "$ref": "#/components/schemas/Envelope"
}
```

Example payload:

```json
{
  "status": 200,
  "message": ""
}
```

Error responses:

#### 500

Error response.

No response payload documented.

### GET /v1/accounts/{accountId}/signers

List signers. Security: **bearerAuth or apiKeyAuth**.

List the signers of a workspace.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `search` | query | no | `{"type":"string"}` | Filter by full_name or email. |
| `page` | query | no | `{"type":"integer","minimum":1}` | Page number. |
| `per-page` | query | no | `{"type":"integer","maximum":100}` | Records per page (max 100). |

Request body: none.

Success response:
A page of signers

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/Signer"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "resource": "signer",
      "id": "62d6ee35c7741ca4006b9e11",
      "full_name": "John Signer",
      "email": "john@example.com",
      "whatsapp_phone_number": "+5548999990000",
      "has_accepted_terms": false
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/accounts/{accountId}/signers

Create signer. Security: **bearerAuth or apiKeyAuth**.

Create a signer in the workspace.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "full_name"
  ],
  "properties": {
    "full_name": {
      "type": "string",
      "example": "John Dove"
    },
    "email": {
      "type": "string",
      "format": "email",
      "example": "john@example.com"
    },
    "whatsapp_phone_number": {
      "description": "E.164; normalized on save.",
      "type": "string",
      "example": "+5548999990000"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "full_name": "John Dove",
  "email": "john@example.com",
  "whatsapp_phone_number": "+5548999990000"
}
```

Success response:
The created signer

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Signer"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "signer",
    "id": "62d6ee35c7741ca4006b9e11",
    "full_name": "John Signer",
    "email": "john@example.com",
    "whatsapp_phone_number": "+5548999990000",
    "has_accepted_terms": false
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/accounts/{accountId}/signers/{signerId}

Get signer. Security: **bearerAuth or apiKeyAuth**.

Retrieve a signer's information.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `signerId` | path | yes | `{"type":"string"}` | The signer ID. |

Request body: none.

Success response:
The signer

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Signer"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "signer",
    "id": "62d6ee35c7741ca4006b9e11",
    "full_name": "John Signer",
    "email": "john@example.com",
    "whatsapp_phone_number": "+5548999990000",
    "has_accepted_terms": false
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### PUT /v1/accounts/{accountId}/signers/{signerId}

Update signer. Security: **bearerAuth or apiKeyAuth**.

Update a signer's information.

**Verification integrity:** `email` / `whatsapp_phone_number` cannot be changed while the signer has verified that channel on an in-flight (not yet certificated) document — the response is `400` naming the offending document(s). Already-certificated documents do not block updates. Changing a channel that has *unverified* in-flight requests rotates their access/verification codes (invalidating previously sent links/OTPs); use the resend endpoint to redeliver. `full_name` can always be updated.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `signerId` | path | yes | `{"type":"string"}` | The signer ID. |

Request body: required.
`application/json` schema:

```json
{
  "properties": {
    "full_name": {
      "type": "string",
      "example": "John Dove"
    },
    "email": {
      "type": "string",
      "format": "email",
      "example": "john@example.com"
    },
    "whatsapp_phone_number": {
      "description": "E.164; normalized on save.",
      "type": "string",
      "example": "+5548999990000"
    },
    "government_id": {
      "description": "Signer's CPF/CNPJ; digits only on save.",
      "type": "string",
      "example": "39053344705"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "full_name": "John Dove",
  "email": "john@example.com",
  "whatsapp_phone_number": "+5548999990000",
  "government_id": "39053344705"
}
```

Success response:
The updated signer

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Signer"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "signer",
    "id": "62d6ee35c7741ca4006b9e11",
    "full_name": "John Signer",
    "email": "john@example.com",
    "whatsapp_phone_number": "+5548999990000",
    "has_accepted_terms": false
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### DELETE /v1/accounts/{accountId}/signers/{signerId}

Delete signer. Security: **bearerAuth or apiKeyAuth**.

Delete a signer.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `signerId` | path | yes | `{"type":"string"}` | The signer ID. |

Request body: none.

Success response:
Signer deleted

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": [],
          "example": []
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": []
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/signers/self

Get current signer. Security: **signerAccessCode**.

Return the signer identified by the signer access code, including the `has_signature`/`has_initial`/`is_signature_reusable` flags.

Parameters: none.

Request body: none.

Success response:
The signer

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/SignerSelf"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "signer",
    "id": "62d6ee35c7741ca4006b9e11",
    "full_name": "John Signer",
    "email": "john@example.com",
    "whatsapp_phone_number": "+5548999990000",
    "has_accepted_terms": false,
    "has_signature": true,
    "has_initial": false,
    "is_signature_reusable": false
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/signers/{signerId}/document

Get signer's document. Security: **signerAccessCode**.

Return the document and the signer's assignment items, scoped to the signer access code.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `signerId` | path | yes | `{"type":"string"}` | The signer ID. |

Request body: none.

Success response:
The document with the signer's items

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Document"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "document",
    "id": "615601fab04c0a3147bb1246",
    "account_id": "d199996981dbd199996981db",
    "template_id": "string",
    "name": "document.pdf",
    "status": "metadata_ready",
    "artifacts": {
      "original": "https://api.assinafy.com.br/v1/documents/doc1/download/original"
    },
    "is_closed": false,
    "signing_url": "https://api.assinafy.com.br/v1/sign/doc1",
    "decline_reason": "string",
    "declined_by": {
      "resource": "signer",
      "id": "62d6ee35c7741ca4006b9e11",
      "full_name": "John Signer",
      "email": "john@example.com",
      "whatsapp_phone_number": "+5548999990000",
      "has_accepted_terms": false
    },
    "tags": [
      {
        "id": "string",
        "name": "string"
      }
    ],
    "assignment": {
      "resource": "assignment",
      "id": "615606ef81d199996981dbce",
      "sender_email": "sender@example.com",
      "method": "virtual",
      "expires_at": "2026-08-19T12:00:00Z",
      "message": "string",
      "signers": [
        {
          "resource": "signer",
          "id": "62d6ee35c7741ca4006b9e11",
          "full_name": "John Signer",
          "email": "john@example.com",
          "whatsapp_phone_number": "+5548999990000",
          "has_accepted_terms": false,
          "verification_method": "Email",
          "notification_methods": [
            "Email"
          ],
          "step": 1,
          "notified": false,
          "completed": false,
          "notification_history": [
            {
              "event": "signature_request",
              "status": "sent",
              "error_code": "string",
              "error_message": "string",
              "sent_at": "2026-07-07T12:00:00Z",
              "failed_at": "2026-08-19T12:00:00Z"
            }
          ]
        }
      ],
      "copy_receivers": [
        {}
      ],
      "items": [
        {
          "id": "string",
          "page": {
            "id": "615601faf166d6d1d8e7dc30",
            "number": 1,
            "height": 2100,
            "width": 1275,
            "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
          },
          "signer": {},
          "field": {},
          "display_settings": {},
          "value": {},
          "completed": false
        }
      ],
      "summary": {
        "signer_count": 0,
        "completed_count": 0,
        "signers": [
          {}
        ]
      },
      "signing_urls": [
        {
          "signer_id": "string",
          "url": "https://api.assinafy.com.br/v1/sign/doc1?email=joe@example.com"
        }
      ]
    },
    "pages": [
      {
        "id": "615601faf166d6d1d8e7dc30",
        "number": 1,
        "height": 2100,
        "width": 1275,
        "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
      }
    ],
    "created_at": "2026-06-03T03:54:16Z",
    "updated_at": "2026-06-03T03:54:16Z"
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/sign

View document to sign. Security: **signerAccessCode**.

Retrieve the document a signer has been invited to sign, using the signer access code. Marks the document as viewed. Returns 409 while the document is still being prepared (retry with backoff).

**Signers whose verification method is `DigitalCertificate`** must have confirmed their data *and* accepted the terms before this returns the document; otherwise it is `400`. Both are satisfied in one call to `PUT /v1/documents/{documentId}/signers/confirm-data` with `has_accepted_terms: true`, so send that before this endpoint — the `has_accepted_terms` query parameter here is too late to open the gate. `PUT /v1/signers/accept-terms` also works and is never gated.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `has_accepted_terms` | query | no | `{"type":"boolean"}` | Set true to record terms acceptance. |

Request body: none.

Success response:
The document with the signer's assignment

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Document"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "document",
    "id": "615601fab04c0a3147bb1246",
    "account_id": "d199996981dbd199996981db",
    "template_id": "string",
    "name": "document.pdf",
    "status": "metadata_ready",
    "artifacts": {
      "original": "https://api.assinafy.com.br/v1/documents/doc1/download/original"
    },
    "is_closed": false,
    "signing_url": "https://api.assinafy.com.br/v1/sign/doc1",
    "decline_reason": "string",
    "declined_by": {
      "resource": "signer",
      "id": "62d6ee35c7741ca4006b9e11",
      "full_name": "John Signer",
      "email": "john@example.com",
      "whatsapp_phone_number": "+5548999990000",
      "has_accepted_terms": false
    },
    "tags": [
      {
        "id": "string",
        "name": "string"
      }
    ],
    "assignment": {
      "resource": "assignment",
      "id": "615606ef81d199996981dbce",
      "sender_email": "sender@example.com",
      "method": "virtual",
      "expires_at": "2026-08-19T12:00:00Z",
      "message": "string",
      "signers": [
        {
          "resource": "signer",
          "id": "62d6ee35c7741ca4006b9e11",
          "full_name": "John Signer",
          "email": "john@example.com",
          "whatsapp_phone_number": "+5548999990000",
          "has_accepted_terms": false,
          "verification_method": "Email",
          "notification_methods": [
            "Email"
          ],
          "step": 1,
          "notified": false,
          "completed": false,
          "notification_history": [
            {
              "event": "signature_request",
              "status": "sent",
              "error_code": "string",
              "error_message": "string",
              "sent_at": "2026-07-07T12:00:00Z",
              "failed_at": "2026-08-19T12:00:00Z"
            }
          ]
        }
      ],
      "copy_receivers": [
        {}
      ],
      "items": [
        {
          "id": "string",
          "page": {
            "id": "615601faf166d6d1d8e7dc30",
            "number": 1,
            "height": 2100,
            "width": 1275,
            "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
          },
          "signer": {},
          "field": {},
          "display_settings": {},
          "value": {},
          "completed": false
        }
      ],
      "summary": {
        "signer_count": 0,
        "completed_count": 0,
        "signers": [
          {}
        ]
      },
      "signing_urls": [
        {
          "signer_id": "string",
          "url": "https://api.assinafy.com.br/v1/sign/doc1?email=joe@example.com"
        }
      ]
    },
    "pages": [
      {
        "id": "615601faf166d6d1d8e7dc30",
        "number": 1,
        "height": 2100,
        "width": 1275,
        "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
      }
    ],
    "created_at": "2026-06-03T03:54:16Z",
    "updated_at": "2026-06-03T03:54:16Z"
  }
}
```

Error responses:

#### 400

A digital-certificate signer has not yet confirmed their data or accepted the terms.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 409

The document is not ready to be viewed yet.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/documents/{documentId}/assignments/{assignmentId}

Sign assignment items. Security: **signerAccessCode**.

Sign a document with input fields (collect method): submit the signer's item values, completing their items. For **virtual** assignments the signer must first confirm their data via `PUT /v1/documents/{documentId}/signers/confirm-data`, otherwise this returns `400` (Signer data must be confirmed before signing). Signers whose verification method is `DigitalCertificate` cannot use this endpoint — their signature must be produced through `POST /v1/signers/certificate/start` + `/complete`, and this returns `400`. The request body is a JSON array of item entries. Uses the signer access code.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentId` | path | yes | `{"type":"string"}` | Document ID. |
| `assignmentId` | path | yes | `{"type":"string"}` | The assignment ID. |

Request body: required.
`application/json` schema:

```json
{
  "type": "array",
  "items": {
    "required": [
      "itemId",
      "fieldId",
      "pageId",
      "value"
    ],
    "properties": {
      "itemId": {
        "description": "The assignment item ID.",
        "type": "string",
        "example": "615606efcde1a39c9d21e30e"
      },
      "fieldId": {
        "description": "Field associated with the item.",
        "type": "string",
        "example": "6152120297080d55bdd13197"
      },
      "pageId": {
        "description": "The page ID.",
        "type": "string",
        "example": "615213ed81b071f4293b2fc2"
      },
      "value": {
        "description": "String representation of the value.",
        "type": "string",
        "example": "Signed by Sonny Bayer"
      }
    },
    "type": "object"
  }
}
```

Example payload:

```json
[
  {
    "itemId": "615606efcde1a39c9d21e30e",
    "fieldId": "6152120297080d55bdd13197",
    "pageId": "615213ed81b071f4293b2fc2",
    "value": "Signed by Sonny Bayer"
  }
]
```

Success response:
Signing result

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "object"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {}
}
```

Error responses:

#### 400

Signer data must be confirmed before signing (virtual assignments), or the signer must sign with a digital certificate through the digital certificate endpoints.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 409

The document is not ready to be signed yet.

No response payload documented.

#### 500

Error response.

No response payload documented.

### PUT /v1/documents/{documentId}/assignments/{assignmentId}/reject

Reject (decline) assignment. Security: **signerAccessCode**.

The signer declines to sign the document, giving a reason. Uses the signer access code.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentId` | path | yes | `{"type":"string"}` | Document ID. |
| `assignmentId` | path | yes | `{"type":"string"}` | The assignment ID. |

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "decline_reason"
  ],
  "properties": {
    "decline_reason": {
      "description": "Descriptive reason for declining.",
      "type": "string",
      "example": "I do not agree with clause 2."
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "decline_reason": "I do not agree with clause 2."
}
```

Success response:
Assignment declined

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "description": "Empty array.",
          "type": "array",
          "items": []
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {}
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### PUT /v1/signers/documents/sign-multiple

Sign multiple documents. Security: **signerAccessCode**.

Sign several documents in one request, for a signer with multiple pending documents. Each document must be prepared for the **virtual** signature method. Uses the signer access code.

Parameters: none.

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "document_ids"
  ],
  "properties": {
    "document_ids": {
      "description": "IDs of the documents to sign.",
      "type": "array",
      "items": {
        "type": "string"
      },
      "example": [
        "documentid1",
        "documentid2"
      ]
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "document_ids": [
    "documentid1",
    "documentid2"
  ]
}
```

Success response:
Signing result

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "description": "Empty array.",
          "type": "array",
          "items": []
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {}
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### PUT /v1/signers/documents/decline-multiple

Decline multiple documents. Security: **signerAccessCode**.

Decline several documents in one request. Uses the signer access code.

Parameters: none.

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "document_ids",
    "decline_reason"
  ],
  "properties": {
    "document_ids": {
      "description": "IDs of the documents to decline.",
      "type": "array",
      "items": {
        "type": "string"
      },
      "example": [
        "documentid1",
        "documentid2"
      ]
    },
    "decline_reason": {
      "description": "Reason for declining.",
      "type": "string",
      "example": "Unfavorable terms."
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "document_ids": [
    "documentid1",
    "documentid2"
  ],
  "decline_reason": "Unfavorable terms."
}
```

Success response:
Decline result

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "description": "Empty array.",
          "type": "array",
          "items": []
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {}
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/verify

Verify signer code (OTP). Security: **signerAccessCode**.

Submit the verification code (OTP) sent to the signer to unlock the signing flow. Uses the signer access code.

Parameters: none.

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "verification-code"
  ],
  "properties": {
    "verification-code": {
      "type": "string",
      "example": "123456"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "verification-code": "123456"
}
```

Success response:
Code verified

`application/json` schema:

```json
{
  "$ref": "#/components/schemas/Envelope"
}
```

Example payload:

```json
{
  "status": 200,
  "message": ""
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### PUT /v1/documents/{documentId}/signers/confirm-data

Confirm signer data. Security: **signerAccessCode**.

The signer confirms or updates their data before signing. Uses the signer access code.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentId` | path | yes | `{"type":"string"}` | Document ID. |

Request body: required.
`application/json` schema:

```json
{
  "properties": {
    "full_name": {
      "type": "string"
    },
    "email": {
      "type": "string",
      "format": "email"
    },
    "government_id": {
      "type": "string"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "full_name": "string",
  "email": "user@example.com",
  "government_id": "string"
}
```

Success response:
Data confirmed

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Signer"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "signer",
    "id": "62d6ee35c7741ca4006b9e11",
    "full_name": "John Signer",
    "email": "john@example.com",
    "whatsapp_phone_number": "+5548999990000",
    "has_accepted_terms": false
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### PUT /v1/signers/accept-terms

Accept terms (signer). Security: **signerAccessCode**.

Record that the signer accepted the terms of use. Uses the signer access code.

Parameters: none.

Request body: none.

Success response:
Terms accepted

`application/json` schema:

```json
{
  "$ref": "#/components/schemas/Envelope"
}
```

Example payload:

```json
{
  "status": 200,
  "message": ""
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/signature

Upload signature image. Security: **signerAccessCode**.

Upload the signer's signature (or initials) image as the raw request body. Uses the signer access code.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `type` | query | no | `{"type":"string"}` | Image type, e.g. `signature` or `initial`. |
| `reuse` | query | no | `{"type":"boolean"}` | Whether the signer opted to reuse this signature in future processes. When set, updates the signer's `is_signature_reusable` flag; when omitted, the flag is left unchanged. |

Request body: required.
`image/png` schema:

```json
{
  "type": "string",
  "format": "binary"
}
```

Success response:
Signature stored

`application/json` schema:

```json
{
  "$ref": "#/components/schemas/Envelope"
}
```

Example payload:

```json
{
  "status": 200,
  "message": ""
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/signature/{signatureType}

Download signature image. Security: **signerAccessCode**.

Download the signer's stored signature/initials image. Uses the signer access code.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `signatureType` | path | yes | `{"type":"string"}` | Image type (e.g. `signature`, `initial`). |

Request body: none.

Success response:
The signature image

`image/*` schema:

```json
{
  "type": "string",
  "format": "binary"
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/signers/{signerId}/documents

List signer's documents. Security: **signerAccessCode**.

List the documents a signer is party to. Uses the signer access code.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `signerId` | path | yes | `{"type":"string"}` | The signer ID. |
| `page` | query | no | `{"type":"integer","minimum":1}` | Page number. |
| `per-page` | query | no | `{"type":"integer","maximum":100}` | Records per page (max 100). |

Request body: none.

Success response:
The signer's documents

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/Document"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "resource": "document",
      "id": "615601fab04c0a3147bb1246",
      "account_id": "d199996981dbd199996981db",
      "template_id": "string",
      "name": "document.pdf",
      "status": "metadata_ready",
      "artifacts": {
        "original": "https://api.assinafy.com.br/v1/documents/doc1/download/original"
      },
      "is_closed": false,
      "signing_url": "https://api.assinafy.com.br/v1/sign/doc1",
      "decline_reason": "string",
      "declined_by": {
        "resource": "signer",
        "id": "62d6ee35c7741ca4006b9e11",
        "full_name": "John Signer",
        "email": "john@example.com",
        "whatsapp_phone_number": "+5548999990000",
        "has_accepted_terms": false
      },
      "tags": [
        {
          "id": "string",
          "name": "string"
        }
      ],
      "assignment": {
        "resource": "assignment",
        "id": "615606ef81d199996981dbce",
        "sender_email": "sender@example.com",
        "method": "virtual",
        "expires_at": "2026-08-19T12:00:00Z",
        "message": "string",
        "signers": [
          {
            "resource": "signer",
            "id": "62d6ee35c7741ca4006b9e11",
            "full_name": "John Signer",
            "email": "john@example.com",
            "whatsapp_phone_number": "+5548999990000",
            "has_accepted_terms": false,
            "verification_method": "Email",
            "notification_methods": [
              "Email"
            ],
            "step": 1,
            "notified": false,
            "completed": false,
            "notification_history": [
              {
                "event": "signature_request",
                "status": "sent",
                "error_code": "string",
                "error_message": "string",
                "sent_at": "2026-07-07T12:00:00Z",
                "failed_at": "2026-08-19T12:00:00Z"
              }
            ]
          }
        ],
        "copy_receivers": [
          {}
        ],
        "items": [
          {
            "id": "string",
            "page": {
              "id": "615601faf166d6d1d8e7dc30",
              "number": 1,
              "height": 2100,
              "width": 1275,
              "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
            },
            "signer": {},
            "field": {},
            "display_settings": {},
            "value": {},
            "completed": false
          }
        ],
        "summary": {
          "signer_count": 0,
          "completed_count": 0,
          "signers": [
            {}
          ]
        },
        "signing_urls": [
          {
            "signer_id": "string",
            "url": "https://api.assinafy.com.br/v1/sign/doc1?email=joe@example.com"
          }
        ]
      },
      "pages": [
        {
          "id": "615601faf166d6d1d8e7dc30",
          "number": 1,
          "height": 2100,
          "width": 1275,
          "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
        }
      ],
      "created_at": "2026-06-03T03:54:16Z",
      "updated_at": "2026-06-03T03:54:16Z"
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/signers/{signerId}/documents/search

Search signer's documents. Security: **signerAccessCode**.

Search the documents a signer is party to (compact representation). Uses the signer access code.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `signerId` | path | yes | `{"type":"string"}` | The signer ID. |
| `search` | query | no | `{"type":"string"}` | Search term. |

Request body: none.

Success response:
Matching documents

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/Document"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "resource": "document",
      "id": "615601fab04c0a3147bb1246",
      "account_id": "d199996981dbd199996981db",
      "template_id": "string",
      "name": "document.pdf",
      "status": "metadata_ready",
      "artifacts": {
        "original": "https://api.assinafy.com.br/v1/documents/doc1/download/original"
      },
      "is_closed": false,
      "signing_url": "https://api.assinafy.com.br/v1/sign/doc1",
      "decline_reason": "string",
      "declined_by": {
        "resource": "signer",
        "id": "62d6ee35c7741ca4006b9e11",
        "full_name": "John Signer",
        "email": "john@example.com",
        "whatsapp_phone_number": "+5548999990000",
        "has_accepted_terms": false
      },
      "tags": [
        {
          "id": "string",
          "name": "string"
        }
      ],
      "assignment": {
        "resource": "assignment",
        "id": "615606ef81d199996981dbce",
        "sender_email": "sender@example.com",
        "method": "virtual",
        "expires_at": "2026-08-19T12:00:00Z",
        "message": "string",
        "signers": [
          {
            "resource": "signer",
            "id": "62d6ee35c7741ca4006b9e11",
            "full_name": "John Signer",
            "email": "john@example.com",
            "whatsapp_phone_number": "+5548999990000",
            "has_accepted_terms": false,
            "verification_method": "Email",
            "notification_methods": [
              "Email"
            ],
            "step": 1,
            "notified": false,
            "completed": false,
            "notification_history": [
              {
                "event": "signature_request",
                "status": "sent",
                "error_code": "string",
                "error_message": "string",
                "sent_at": "2026-07-07T12:00:00Z",
                "failed_at": "2026-08-19T12:00:00Z"
              }
            ]
          }
        ],
        "copy_receivers": [
          {}
        ],
        "items": [
          {
            "id": "string",
            "page": {
              "id": "615601faf166d6d1d8e7dc30",
              "number": 1,
              "height": 2100,
              "width": 1275,
              "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
            },
            "signer": {},
            "field": {},
            "display_settings": {},
            "value": {},
            "completed": false
          }
        ],
        "summary": {
          "signer_count": 0,
          "completed_count": 0,
          "signers": [
            {}
          ]
        },
        "signing_urls": [
          {
            "signer_id": "string",
            "url": "https://api.assinafy.com.br/v1/sign/doc1?email=joe@example.com"
          }
        ]
      },
      "pages": [
        {
          "id": "615601faf166d6d1d8e7dc30",
          "number": 1,
          "height": 2100,
          "width": 1275,
          "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
        }
      ],
      "created_at": "2026-06-03T03:54:16Z",
      "updated_at": "2026-06-03T03:54:16Z"
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/signers/{signerId}/documents/{documentId}/download/{artifactName}

Download signer's document artifact. Security: **public**.

Download an artifact of a document the signer is party to. Public (signer-link) endpoint. Artifact types: original, certificated, certificate-page, pades, bundle. The pades artifact (signers' ICP-Brasil signatures + platform certification box) is only present on documents that had digital-certificate signers; `bundle` is a zip of the original, certificated and certificate-page artifacts, plus the pades artifact on documents that have one.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `signerId` | path | yes | `{"type":"string"}` | The signer ID. |
| `documentId` | path | yes | `{"type":"string"}` | Document ID. |
| `artifactName` | path | yes | `{"type":"string","enum":["original","certificated","certificate-page","pades","bundle"]}` | Artifact type. |

Request body: none.

Success response:
The artifact binary

`application/pdf` schema:

```json
{
  "type": "string",
  "format": "binary"
}
```

Error responses:

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/authentication/social-login

Social login. Security: **public**.

Exchange a token from a social login provider (currently only `google`) for an Assinafy access token.

Parameters: none.

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "provider",
    "token",
    "has_accepted_terms"
  ],
  "properties": {
    "provider": {
      "type": "string",
      "enum": [
        "google"
      ],
      "example": "google"
    },
    "token": {
      "description": "Access/ID token from the provider.",
      "type": "string",
      "example": "example-provider-token"
    },
    "has_accepted_terms": {
      "type": "boolean",
      "example": true
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "provider": "google",
  "token": "example-provider-token",
  "has_accepted_terms": true
}
```

Success response:
Access token, user and accounts

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/AuthSession"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "access_token": "example-access-token",
    "user": {
      "id": "bgjazeo5r9v2lq7l36dx48np",
      "name": "John Smith",
      "email": "example@example.com",
      "telephone": "17989206641",
      "government_id": "15774136604",
      "is_email_verified": false,
      "has_accepted_terms": true,
      "created_at": "2023-03-03T11:51:34Z",
      "to_be_deleted_at": "2026-08-19T12:00:00Z"
    },
    "accounts": [
      {
        "id": "6401df46d6a6b0c692d9ec49",
        "name": "JS",
        "roles": [
          "owner"
        ],
        "is_delete_allowed": true,
        "created_at": "2023-03-03T11:51:34Z"
      }
    ]
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/auth/link-social-login

Link social login. Security: **bearerAuth or apiKeyAuth**.

Link a social-login provider account to the authenticated user.

Parameters: none.

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "provider",
    "token"
  ],
  "properties": {
    "provider": {
      "type": "string",
      "enum": [
        "google"
      ],
      "example": "google"
    },
    "token": {
      "description": "Token from the provider.",
      "type": "string"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "provider": "google",
  "token": "string"
}
```

Success response:
Provider linked

`application/json` schema:

```json
{
  "$ref": "#/components/schemas/Envelope"
}
```

Example payload:

```json
{
  "status": 200,
  "message": ""
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/accounts/{accountId}/stats

Account document KPIs. Security: **bearerAuth or apiKeyAuth**.

Precomputed per-account document-funnel KPIs. `granularity=monthly` (default) returns the last 12 months, most recent first; `granularity=daily` with `month=YYYY-MM` returns that month's days. Series are zero-filled.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `granularity` | query | no | `{"type":"string","enum":["monthly","daily"]}` | `monthly` (default) or `daily`. |
| `month` | query | no | `{"type":"string"}` | Target month `YYYY-MM` (required when `granularity=daily`). |

Request body: none.

Success response:
KPI series

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/DocumentStatsRow"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "period": "2026-06",
      "documents_uploaded": 42,
      "documents_sent": 37,
      "signature_requests": 61,
      "signature_requests_notification_email": 55,
      "signature_requests_notification_whatsapp": 18,
      "signature_requests_notification_bypass": 3,
      "signature_requests_verification_email": 48,
      "signature_requests_verification_whatsapp": 6,
      "signature_requests_verification_bypass": 3,
      "signature_requests_verification_digital_certificate": 4,
      "signature_requests_viewed": 44,
      "signature_requests_completed": 52,
      "documents_certified": 30
    }
  ]
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/users/self/stats

My cross-account document KPIs. Security: **bearerAuth or apiKeyAuth**.

The authenticated user's document-funnel KPIs summed across all accounts they currently belong to. `granularity=monthly` (default) returns the last 12 months, most recent first; `granularity=daily` with `month=YYYY-MM` returns that month's days. Series are zero-filled.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `granularity` | query | no | `{"type":"string","enum":["monthly","daily"]}` | `monthly` (default) or `daily`. |
| `month` | query | no | `{"type":"string"}` | Target month `YYYY-MM` (required when `granularity=daily`). |

Request body: none.

Success response:
KPI series

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/DocumentStatsRow"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "period": "2026-06",
      "documents_uploaded": 42,
      "documents_sent": 37,
      "signature_requests": 61,
      "signature_requests_notification_email": 55,
      "signature_requests_notification_whatsapp": 18,
      "signature_requests_notification_bypass": 3,
      "signature_requests_verification_email": 48,
      "signature_requests_verification_whatsapp": 6,
      "signature_requests_verification_bypass": 3,
      "signature_requests_verification_digital_certificate": 4,
      "signature_requests_viewed": 44,
      "signature_requests_completed": 52,
      "documents_certified": 30
    }
  ]
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/accounts/{accountId}/tags

List tags. Security: **bearerAuth or apiKeyAuth**.

List the tags of a workspace.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `search` | query | no | `{"type":"string"}` | Search term. |

Request body: none.

Success response:
The workspace tags

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/Tag"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "resource": "tag",
      "id": "fa8c09f3e709a8a1c82d69b1454",
      "name": "Contracts",
      "color": "ff8800",
      "created_at": "2026-05-14T12:00:00Z",
      "updated_at": "2026-05-14T12:00:00Z"
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/accounts/{accountId}/tags

Create tag. Security: **bearerAuth or apiKeyAuth**.

Create a tag in the workspace. Names are unique per workspace (case-insensitive); a collision returns 409.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "name"
  ],
  "properties": {
    "name": {
      "description": "Trimmed; whitespace collapsed; max 64 chars.",
      "type": "string",
      "example": "Contracts"
    },
    "color": {
      "description": "6-char hex (with or without leading #).",
      "type": "string",
      "example": "ff8800",
      "nullable": true
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "name": "Contracts",
  "color": "ff8800"
}
```

Success response:
The created tag

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Tag"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "tag",
    "id": "fa8c09f3e709a8a1c82d69b1454",
    "name": "Contracts",
    "color": "ff8800",
    "created_at": "2026-05-14T12:00:00Z",
    "updated_at": "2026-05-14T12:00:00Z"
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 409

A tag with the same name already exists.

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/ErrorEnvelope"
    },
    {
      "properties": {
        "status": {
          "type": "integer",
          "example": 409
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 409,
  "message": "Bad request.",
  "data": {}
}
```

#### 500

Error response.

No response payload documented.

### PUT /v1/accounts/{accountId}/tags/{tagId}

Update tag. Security: **bearerAuth or apiKeyAuth**.

Update a tag's name or color.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `tagId` | path | yes | `{"type":"string"}` | The tag ID. |

Request body: required.
`application/json` schema:

```json
{
  "properties": {
    "name": {
      "type": "string",
      "example": "Signed Contracts"
    },
    "color": {
      "type": "string",
      "example": "00aa55",
      "nullable": true
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "name": "Signed Contracts",
  "color": "00aa55"
}
```

Success response:
The updated tag

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Tag"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "tag",
    "id": "fa8c09f3e709a8a1c82d69b1454",
    "name": "Contracts",
    "color": "ff8800",
    "created_at": "2026-05-14T12:00:00Z",
    "updated_at": "2026-05-14T12:00:00Z"
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### DELETE /v1/accounts/{accountId}/tags/{tagId}

Delete tag. Security: **bearerAuth or apiKeyAuth**.

Delete a tag. Pass `?force=true` to detach it from any documents/templates it is attached to.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `tagId` | path | yes | `{"type":"string"}` | The tag ID. |
| `force` | query | no | `{"type":"boolean"}` | Detach from resources before deleting. |

Request body: none.

Success response:
Tag deleted

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "properties": {
            "deleted": {
              "type": "boolean",
              "example": true
            }
          },
          "type": "object"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "deleted": true
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/accounts/{accountId}/templates

List templates. Security: **bearerAuth or apiKeyAuth**.

List the templates of a workspace.

The `status` field of a template is one of:

| Status | Description |
|--------|-------------|
| `uploading` | The template is being uploaded. |
| `uploaded` | The template has been uploaded. |
| `processing` | The template is being processed. |
| `ready` | The template is ready to use. |
| `failed` | The template processing has failed. |

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `search` | query | no | `{"type":"string"}` | Search term. |
| `page` | query | no | `{"type":"integer","minimum":1}` | Page number. |
| `per-page` | query | no | `{"type":"integer","maximum":100}` | Records per page (max 100). |

Request body: none.

Success response:
A page of templates (default_document_tags omitted in the list)

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/Template"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "resource": "template",
      "id": "fa88b732db84d01427d4cdd1092",
      "name": "template.pdf",
      "document_name": "string",
      "message": "string",
      "status": "ready",
      "pages": [
        {
          "id": "string",
          "number": 1,
          "height": 2100,
          "width": 1275,
          "download_url": "string",
          "fields": [
            {
              "id": "string",
              "field_id": "string",
              "role_id": "string",
              "label": "string",
              "display_settings": {},
              "created_at": "2026-08-19T12:00:00Z",
              "updated_at": "2026-08-19T12:00:00Z"
            }
          ]
        }
      ],
      "roles": [
        {
          "id": "string",
          "name": "Editor",
          "assignment_type": "Editor",
          "created_at": "2026-08-19T12:00:00Z",
          "updated_at": "2026-08-19T12:00:00Z"
        }
      ],
      "tags": [
        {
          "id": "string",
          "name": "string"
        }
      ],
      "default_document_tags": [
        {
          "id": "string",
          "name": "string"
        }
      ],
      "created_at": "2026-08-19T12:00:00Z",
      "updated_at": "2026-08-19T12:00:00Z"
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/accounts/{accountId}/templates/{templateId}/documents

Create document from template. Security: **bearerAuth or apiKeyAuth**.

Generate a new document from a template, creating its assignment in the same call. Provide one signer entry per template role; the signers must already exist in the account.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `templateId` | path | yes | `{"type":"string"}` | The template ID. |

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "signers"
  ],
  "properties": {
    "signers": {
      "description": "One entry per template role.",
      "type": "array",
      "items": {
        "required": [
          "role_id",
          "id"
        ],
        "properties": {
          "role_id": {
            "description": "The template role ID associated with the signer.",
            "type": "string",
            "example": "fa8c14f32d732271e071998246e"
          },
          "id": {
            "description": "The signer ID. The signer must already exist in the account.",
            "type": "string",
            "example": "fa8c140cb49b79f940aab95fddd"
          },
          "verification_method": {
            "description": "Verification method for this signer. If provided without notification_methods, the notification method is inferred. Defaults to Email. `DigitalCertificate` has the signer sign with their own ICP-Brasil certificate — it requires the Digital Certificate feature, the signer must have a CPF or CNPJ in `government_id` (a CPF requires that person's certificate; a CNPJ requires an e-CNPJ for that company), must be alone in its signing step, and is charged 2 credits per signer. Only applies to signers (not copy receivers).",
            "type": "string",
            "enum": [
              "Email",
              "Whatsapp",
              "DigitalCertificate"
            ],
            "example": "Email"
          },
          "notification_methods": {
            "description": "Notification method codes for this signer. If provided without verification_method, the verification method is inferred. Defaults to Email. Only one method allowed per signer.",
            "type": "array",
            "items": {
              "type": "string"
            },
            "example": [
              "Email"
            ]
          },
          "step": {
            "description": "Positive integer that controls signing order. Signers sharing the same step sign in parallel; a step activates only after every signer in the previous step has signed. If supplied for any role it must be supplied for all, forming a contiguous sequence starting at 1. Copy receivers ignore this field.",
            "type": "integer",
            "example": 1
          }
        },
        "type": "object"
      }
    },
    "editor_fields": {
      "description": "Editor field values to bake into the generated document.",
      "type": "array",
      "items": {
        "required": [
          "field_id",
          "value"
        ],
        "properties": {
          "field_id": {
            "description": "The field identifier, matching the field_id in the template data.",
            "type": "string",
            "example": "fa8c14f3af99d2846d1789de4ba"
          },
          "value": {
            "description": "The value to assign to the field.",
            "type": "string",
            "example": "Field value"
          }
        },
        "type": "object"
      }
    },
    "name": {
      "description": "Title for the document. Defaults to the template name.",
      "type": "string",
      "example": "sample-contract-one-page.pdf"
    },
    "message": {
      "description": "Optional message sent to signers.",
      "type": "string",
      "example": "Message to the signers"
    },
    "expires_at": {
      "description": "Assignment expiration date (ISO 8601). No expiration by default.",
      "type": "string",
      "format": "date-time",
      "example": "2024-07-30T23:59:00Z"
    },
    "tags": {
      "description": "Tag names to attach to the new document. Names that don't exist are auto-created. The template's default-document-tags are always applied; values here are merged on top (duplicates removed).",
      "type": "array",
      "items": {
        "type": "string"
      }
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "signers": [
    {
      "role_id": "fa8c14f32d732271e071998246e",
      "id": "fa8c140cb49b79f940aab95fddd",
      "verification_method": "Email",
      "notification_methods": [
        "Email"
      ],
      "step": 1
    }
  ],
  "editor_fields": [
    {
      "field_id": "fa8c14f3af99d2846d1789de4ba",
      "value": "Field value"
    }
  ],
  "name": "sample-contract-one-page.pdf",
  "message": "Message to the signers",
  "expires_at": "2024-07-30T23:59:00Z",
  "tags": [
    "string"
  ]
}
```

Success response:
The created document

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/Document"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "document",
    "id": "615601fab04c0a3147bb1246",
    "account_id": "d199996981dbd199996981db",
    "template_id": "string",
    "name": "document.pdf",
    "status": "metadata_ready",
    "artifacts": {
      "original": "https://api.assinafy.com.br/v1/documents/doc1/download/original"
    },
    "is_closed": false,
    "signing_url": "https://api.assinafy.com.br/v1/sign/doc1",
    "decline_reason": "string",
    "declined_by": {
      "resource": "signer",
      "id": "62d6ee35c7741ca4006b9e11",
      "full_name": "John Signer",
      "email": "john@example.com",
      "whatsapp_phone_number": "+5548999990000",
      "has_accepted_terms": false
    },
    "tags": [
      {
        "id": "string",
        "name": "string"
      }
    ],
    "assignment": {
      "resource": "assignment",
      "id": "615606ef81d199996981dbce",
      "sender_email": "sender@example.com",
      "method": "virtual",
      "expires_at": "2026-08-19T12:00:00Z",
      "message": "string",
      "signers": [
        {
          "resource": "signer",
          "id": "62d6ee35c7741ca4006b9e11",
          "full_name": "John Signer",
          "email": "john@example.com",
          "whatsapp_phone_number": "+5548999990000",
          "has_accepted_terms": false,
          "verification_method": "Email",
          "notification_methods": [
            "Email"
          ],
          "step": 1,
          "notified": false,
          "completed": false,
          "notification_history": [
            {
              "event": "signature_request",
              "status": "sent",
              "error_code": "string",
              "error_message": "string",
              "sent_at": "2026-07-07T12:00:00Z",
              "failed_at": "2026-08-19T12:00:00Z"
            }
          ]
        }
      ],
      "copy_receivers": [
        {}
      ],
      "items": [
        {
          "id": "string",
          "page": {
            "id": "615601faf166d6d1d8e7dc30",
            "number": 1,
            "height": 2100,
            "width": 1275,
            "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
          },
          "signer": {},
          "field": {},
          "display_settings": {},
          "value": {},
          "completed": false
        }
      ],
      "summary": {
        "signer_count": 0,
        "completed_count": 0,
        "signers": [
          {}
        ]
      },
      "signing_urls": [
        {
          "signer_id": "string",
          "url": "https://api.assinafy.com.br/v1/sign/doc1?email=joe@example.com"
        }
      ]
    },
    "pages": [
      {
        "id": "615601faf166d6d1d8e7dc30",
        "number": 1,
        "height": 2100,
        "width": 1275,
        "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
      }
    ],
    "created_at": "2026-06-03T03:54:16Z",
    "updated_at": "2026-06-03T03:54:16Z"
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/accounts/{accountId}/templates/{templateId}/documents/estimate-cost

Estimate document-from-template cost. Security: **bearerAuth or apiKeyAuth**.

Estimate the cost of creating a document from a template without creating it. Contact information is not required — only the role_id and optionally a verification or notification method are needed. Each document always consumes 1 document from the plan's monthly allowance; if exhausted, the ExtraDocument cost is charged from credits (needs_extra_document = true). blocking_reason may be PendingPayment, InsufficientDocuments, or InsufficientCredits.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `templateId` | path | yes | `{"type":"string"}` | The template ID. |

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "signers"
  ],
  "properties": {
    "signers": {
      "description": "One entry per template role (editor roles are ignored for cost calculation).",
      "type": "array",
      "items": {
        "required": [
          "role_id"
        ],
        "properties": {
          "role_id": {
            "description": "The template role ID associated with the signer.",
            "type": "string",
            "example": "fa8c14f32d732271e071998246e"
          },
          "verification_method": {
            "description": "Verification method. If provided without notification_methods, the notification method is inferred. Defaults to Email. `DigitalCertificate` adds the per-signer signature cost (2 credits) on top of the notification cost.",
            "type": "string",
            "enum": [
              "Email",
              "Whatsapp",
              "DigitalCertificate"
            ],
            "example": "Whatsapp"
          },
          "notification_methods": {
            "description": "Notification methods. If provided without verification_method, the verification method is inferred. Defaults to Email.",
            "type": "array",
            "items": {
              "type": "string"
            },
            "example": [
              "Whatsapp"
            ]
          }
        },
        "type": "object"
      }
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "signers": [
    {
      "role_id": "fa8c14f32d732271e071998246e",
      "verification_method": "Whatsapp",
      "notification_methods": [
        "Whatsapp"
      ]
    }
  ]
}
```

Success response:
Cost estimate

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/CostEstimate"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "documents": 1,
    "credits": 0,
    "needs_extra_document": false,
    "extra_document_cost": 1,
    "total_credits": 0,
    "breakdown": [
      {
        "code": "NotificationWhatsapp",
        "name": "Whatsapp Notification",
        "cost": 0.9,
        "quantity": 2,
        "unit_cost": 0.45
      }
    ],
    "document_balance": 0,
    "credit_balance": 0,
    "has_sufficient_resources": false,
    "blocking_reason": "PendingPayment",
    "message": "string"
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/users/self

Get the authenticated user. Security: **bearerAuth or apiKeyAuth**.

Returns the profile of the user owning the access token.

Parameters: none.

Request body: none.

Success response:
The current user

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/AuthUser"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "id": "bgjazeo5r9v2lq7l36dx48np",
    "name": "John Smith",
    "email": "example@example.com",
    "telephone": "17989206641",
    "government_id": "15774136604",
    "is_email_verified": false,
    "has_accepted_terms": true,
    "created_at": "2023-03-03T11:51:34Z",
    "to_be_deleted_at": "2026-08-19T12:00:00Z"
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/users/api-keys

Get API key. Security: **bearerAuth or apiKeyAuth**.

Retrieve a masked version of the existing API key. The full key cannot be retrieved. Returns `null` when no key has been generated yet.

Parameters: none.

Request body: none.

Success response:
The masked API key

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/ApiKey"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "api_key": "example-api-key"
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/users/api-keys

Create API key. Security: **bearerAuth or apiKeyAuth**.

Generate an API key for the user, used via the `X-Api-Key` header. Generating a new key deletes the previous one. Never use an API key from a front-end application.

Parameters: none.

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "password"
  ],
  "properties": {
    "password": {
      "description": "The user's password.",
      "type": "string",
      "format": "password",
      "example": "password"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "password": "password"
}
```

Success response:
The generated API key (shown in full only once)

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/ApiKey"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "api_key": "example-api-key"
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### DELETE /v1/users/api-keys

Delete API key. Security: **bearerAuth or apiKeyAuth**.

Delete the existing API key.

Parameters: none.

Request body: none.

Success response:
API key deleted

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": [],
          "example": []
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": []
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/accounts/{accountId}/webhooks/subscriptions

Get webhook subscription. Security: **bearerAuth or apiKeyAuth**.

Retrieve the current webhook subscription for the account — which events it is subscribed to and the delivery configuration.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |

Request body: none.

Success response:
The subscription

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/WebhookSubscription"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "events": [
      "document_ready",
      "document_prepared"
    ],
    "is_active": true,
    "url": "http://example.com?test=1",
    "email": "email@example.com",
    "updated_at": "2023-05-10T14:58:24Z"
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### PUT /v1/accounts/{accountId}/webhooks/subscriptions

Update webhook subscription. Security: **bearerAuth or apiKeyAuth**.

Update the webhook subscription settings for the account — which events are monitored, whether delivery is enabled, and the delivery/contact details.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |

Request body: required.
`application/json` schema:

```json
{
  "required": [
    "events",
    "is_active",
    "url",
    "email"
  ],
  "properties": {
    "events": {
      "description": "Event type codes to subscribe to (see `GET /v1/webhooks/event-types`).",
      "type": "array",
      "items": {
        "type": "string"
      },
      "example": [
        "document_ready",
        "document_prepared"
      ]
    },
    "is_active": {
      "description": "Whether events should be delivered to the webhook.",
      "type": "boolean",
      "example": true
    },
    "url": {
      "description": "The URL that will receive events.",
      "type": "string",
      "format": "uri",
      "example": "http://example.com?test=1"
    },
    "email": {
      "description": "Email that receives important webhook-communication notices.",
      "type": "string",
      "format": "email",
      "example": "email@example.com"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "events": [
    "document_ready",
    "document_prepared"
  ],
  "is_active": true,
  "url": "http://example.com?test=1",
  "email": "email@example.com"
}
```

Success response:
The updated subscription

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/WebhookSubscription"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "events": [
      "document_ready",
      "document_prepared"
    ],
    "is_active": true,
    "url": "http://example.com?test=1",
    "email": "email@example.com",
    "updated_at": "2023-05-10T14:58:24Z"
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### PUT /v1/accounts/{accountId}/webhooks/inactivate

Inactivate webhook subscription. Security: **bearerAuth or apiKeyAuth**.

Deactivate the webhook integration for the account. While inactive, no events are sent to the configured endpoint.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |

Request body: none.

Success response:
The inactivated subscription

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/WebhookSubscription"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "events": [
      "document_ready",
      "document_prepared"
    ],
    "is_active": true,
    "url": "http://example.com?test=1",
    "email": "email@example.com",
    "updated_at": "2023-05-10T14:58:24Z"
  }
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/webhooks/event-types

List webhook event types. Security: **bearerAuth or apiKeyAuth**.

List all available event types that can be subscribed to via webhooks.

Parameters: none.

Request body: none.

Success response:
Event types

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/WebhookEventType"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "id": "document_ready"
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/accounts/{accountId}/webhooks

List webhook deliveries. Security: **bearerAuth or apiKeyAuth**.

Retrieve the delivery history for webhooks sent to the account's configured endpoint — use it to monitor status, debug failures, and verify payloads. Pagination is returned in the `X-Pagination-*` response headers.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `event` | query | no | `{"type":"string"}` | Filter by event type (e.g. `document_ready`). |
| `delivered` | query | no | `{"type":"string","enum":["true","false"]}` | Filter by delivery status: `true` or `false`. |
| `from` | query | no | `{"type":"integer"}` | Unix timestamp — only entries after this time. |
| `to` | query | no | `{"type":"integer"}` | Unix timestamp — only entries before this time. |
| `page` | query | no | `{"type":"integer","default":1}` | Page number. |
| `per-page` | query | no | `{"type":"integer","default":20}` | Items per page (default: 20). |

Request body: none.

Success response:
Delivery history

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/WebhookDispatch"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "resource": "activity_dispatching_history",
      "id": "a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6",
      "event": "document_ready",
      "activity_id": 456,
      "endpoint": "https://example.com/webhook",
      "payload": {},
      "delivered": true,
      "http_status": 200,
      "response_body": "OK",
      "error": "string",
      "created_at": "2024-01-15T10:30:00Z",
      "updated_at": "2024-01-15T10:30:00Z"
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### POST /v1/accounts/{accountId}/webhooks/{historyId}/retry

Retry webhook delivery. Security: **bearerAuth or apiKeyAuth**.

Manually retry a webhook delivery for a specific entry, without waiting for automatic retries. Returns the newly created dispatch entry.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `accountId` | path | yes | `{"type":"string"}` | Workspace account ID. |
| `historyId` | path | yes | `{"type":"string"}` | The webhook dispatch entry ID to retry. |

Request body: none.

Success response:
The new dispatch entry

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "$ref": "#/components/schemas/WebhookDispatch"
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": {
    "resource": "activity_dispatching_history",
    "id": "a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6",
    "event": "document_ready",
    "activity_id": 456,
    "endpoint": "https://example.com/webhook",
    "payload": {},
    "delivered": true,
    "http_status": 200,
    "response_body": "OK",
    "error": "string",
    "created_at": "2024-01-15T10:30:00Z",
    "updated_at": "2024-01-15T10:30:00Z"
  }
}
```

Error responses:

#### 400

Error response.

No response payload documented.

#### 401

Error response.

No response payload documented.

#### 404

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

### GET /v1/documents/{documentId}/assignments/{assignmentId}/whatsapp-notifications

List WhatsApp notifications. Security: **bearerAuth or apiKeyAuth**.

List all WhatsApp notification messages sent for an assignment. The response includes the rendered template text split into `header`, `body` and `buttons` — exactly what the signer would see. In sandbox/stage, WhatsApp messages are simulated (no real delivery) and button URLs include access/verification codes you can use to simulate the signing flow.

| Parameter | Location | Required | Schema | Description |
|---|---|---:|---|---|
| `documentId` | path | yes | `{"type":"string"}` | Document ID. |
| `assignmentId` | path | yes | `{"type":"string"}` | The assignment ID. |

Request body: none.

Success response:
WhatsApp notifications

`application/json` schema:

```json
{
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Envelope"
    },
    {
      "properties": {
        "data": {
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/WhatsappNotification"
          }
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "status": 200,
  "message": "",
  "data": [
    {
      "sent_at": 1710000000,
      "header": "Documento para assinatura: Contrato de Servico",
      "body": "string",
      "buttons": [
        {
          "text": "Abrir documento"
        }
      ],
      "phone_number": "+5511999990001",
      "signer_id": "a51edaee68a7"
    }
  ]
}
```

Error responses:

#### 401

Error response.

No response payload documented.

#### 500

Error response.

No response payload documented.

## Component schemas

### Schema: Envelope

Full schema:

```json
{
  "description": "Standard success wrapper. Operations add their own `data`.",
  "properties": {
    "status": {
      "description": "HTTP status code, mirrored in the body.",
      "type": "integer",
      "example": 200
    },
    "message": {
      "description": "Human-readable message; empty on success.",
      "type": "string",
      "example": ""
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "status": 200,
  "message": ""
}
```

### Schema: ErrorEnvelope

Full schema:

```json
{
  "description": "Standard error wrapper. `status` mirrors the HTTP status code.",
  "properties": {
    "status": {
      "type": "integer",
      "example": 400
    },
    "message": {
      "description": "Human-readable error message.",
      "type": "string",
      "example": "Bad request."
    },
    "data": {
      "type": "object",
      "example": null,
      "nullable": true
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "status": 400,
  "message": "Bad request.",
  "data": {}
}
```

### Schema: ApiKey

Full schema:

```json
{
  "properties": {
    "api_key": {
      "type": "string",
      "example": "example-api-key",
      "nullable": true
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "api_key": "example-api-key"
}
```

### Schema: AuthUser

Full schema:

```json
{
  "properties": {
    "id": {
      "type": "string",
      "example": "bgjazeo5r9v2lq7l36dx48np"
    },
    "name": {
      "type": "string",
      "example": "John Smith"
    },
    "email": {
      "type": "string",
      "format": "email",
      "example": "example@example.com"
    },
    "telephone": {
      "type": "string",
      "example": "17989206641",
      "nullable": true
    },
    "government_id": {
      "type": "string",
      "example": "15774136604",
      "nullable": true
    },
    "is_email_verified": {
      "type": "boolean",
      "example": false
    },
    "has_accepted_terms": {
      "type": "boolean",
      "example": true
    },
    "created_at": {
      "type": "string",
      "format": "date-time",
      "example": "2023-03-03T11:51:34Z"
    },
    "to_be_deleted_at": {
      "type": "string",
      "format": "date-time",
      "example": null,
      "nullable": true
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "id": "bgjazeo5r9v2lq7l36dx48np",
  "name": "John Smith",
  "email": "example@example.com",
  "telephone": "17989206641",
  "government_id": "15774136604",
  "is_email_verified": false,
  "has_accepted_terms": true,
  "created_at": "2023-03-03T11:51:34Z",
  "to_be_deleted_at": "2026-08-19T12:00:00Z"
}
```

### Schema: AuthAccount

Full schema:

```json
{
  "properties": {
    "id": {
      "type": "string",
      "example": "6401df46d6a6b0c692d9ec49"
    },
    "name": {
      "type": "string",
      "example": "JS"
    },
    "roles": {
      "type": "array",
      "items": {
        "type": "string"
      },
      "example": [
        "owner"
      ]
    },
    "is_delete_allowed": {
      "type": "boolean",
      "example": true
    },
    "created_at": {
      "type": "string",
      "format": "date-time",
      "example": "2023-03-03T11:51:34Z"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "id": "6401df46d6a6b0c692d9ec49",
  "name": "JS",
  "roles": [
    "owner"
  ],
  "is_delete_allowed": true,
  "created_at": "2023-03-03T11:51:34Z"
}
```

### Schema: Signer

Full schema:

```json
{
  "description": "A signing party belonging to a workspace account.",
  "properties": {
    "resource": {
      "description": "Present in single-resource responses.",
      "type": "string",
      "example": "signer"
    },
    "id": {
      "type": "string",
      "example": "62d6ee35c7741ca4006b9e11"
    },
    "full_name": {
      "type": "string",
      "example": "John Signer"
    },
    "email": {
      "type": "string",
      "format": "email",
      "example": "john@example.com",
      "nullable": true
    },
    "whatsapp_phone_number": {
      "description": "E.164 format; normalized on save.",
      "type": "string",
      "example": "+5548999990000",
      "nullable": true
    },
    "has_accepted_terms": {
      "type": "boolean",
      "example": false
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "resource": "signer",
  "id": "62d6ee35c7741ca4006b9e11",
  "full_name": "John Signer",
  "email": "john@example.com",
  "whatsapp_phone_number": "+5548999990000",
  "has_accepted_terms": false
}
```

### Schema: SignerSelf

Full schema:

```json
{
  "description": "The current signer, as returned by `GET /v1/signers/self`. Extends Signer with the signature-state flags that are only computed for the authenticated signer.",
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Signer"
    },
    {
      "properties": {
        "has_signature": {
          "description": "Whether the signer has a saved signature image stored.",
          "type": "boolean",
          "example": true
        },
        "has_initial": {
          "description": "Whether the signer has a saved initials image stored.",
          "type": "boolean",
          "example": false
        },
        "is_signature_reusable": {
          "description": "Whether the signer opted to reuse their saved signature/initials in future processes. When false, clients should not pre-render the saved image even if `has_signature`/`has_initial` is true.",
          "type": "boolean",
          "example": false
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "resource": "signer",
  "id": "62d6ee35c7741ca4006b9e11",
  "full_name": "John Signer",
  "email": "john@example.com",
  "whatsapp_phone_number": "+5548999990000",
  "has_accepted_terms": false,
  "has_signature": true,
  "has_initial": false,
  "is_signature_reusable": false
}
```

### Schema: DocumentPage

Full schema:

```json
{
  "properties": {
    "id": {
      "type": "string",
      "example": "615601faf166d6d1d8e7dc30"
    },
    "number": {
      "type": "integer",
      "example": 1
    },
    "height": {
      "type": "integer",
      "example": 2100
    },
    "width": {
      "type": "integer",
      "example": 1275
    },
    "download_url": {
      "type": "string",
      "example": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "id": "615601faf166d6d1d8e7dc30",
  "number": 1,
  "height": 2100,
  "width": 1275,
  "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
}
```

### Schema: DisplaySettings

Full schema:

```json
{
  "description": "A field placement rectangle on a document page. Geometry values are pixels in Assinafy's 150-DPI page image, measured from the upper-left corner. Clients must keep the rectangle within the selected page's width and height; the API does not clamp out-of-bounds values.",
  "required": [
    "left",
    "top",
    "width",
    "height",
    "fontSize"
  ],
  "properties": {
    "left": {
      "description": "Horizontal distance from the page's left edge, in page-image pixels.",
      "type": "number",
      "format": "float",
      "minimum": 0,
      "example": 69
    },
    "top": {
      "description": "Vertical distance from the page's top edge, in page-image pixels.",
      "type": "number",
      "format": "float",
      "minimum": 0,
      "example": 282
    },
    "width": {
      "description": "Width of the placement rectangle, in page-image pixels.",
      "type": "number",
      "format": "float",
      "exclusiveMinimum": true,
      "example": 421,
      "minimum": 0
    },
    "height": {
      "description": "Height of the placement rectangle, in page-image pixels.",
      "type": "number",
      "format": "float",
      "exclusiveMinimum": true,
      "example": 45.86,
      "minimum": 0
    },
    "fontFamily": {
      "description": "Font-family presentation metadata.",
      "type": "string",
      "example": "Arial"
    },
    "fontSize": {
      "description": "Font size in the 150-DPI page-image coordinate system.",
      "type": "number",
      "format": "float",
      "exclusiveMinimum": true,
      "example": 22,
      "minimum": 0
    },
    "backgroundColor": {
      "description": "CSS-compatible background-color presentation metadata.",
      "type": "string",
      "example": "#D5EBFF"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "left": 69,
  "top": 282,
  "width": 421,
  "height": 45.86,
  "fontFamily": "Arial",
  "fontSize": 22,
  "backgroundColor": "#D5EBFF"
}
```

### Schema: DocumentStatus

Full schema:

```json
{
  "properties": {
    "code": {
      "type": "string",
      "example": "metadata_ready"
    },
    "deletable": {
      "type": "boolean",
      "example": true
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "code": "metadata_ready",
  "deletable": true
}
```

### Schema: Document

Full schema:

```json
{
  "description": "A document and its current lifecycle state.",
  "properties": {
    "resource": {
      "description": "Present in single-resource responses.",
      "type": "string",
      "example": "document"
    },
    "id": {
      "type": "string",
      "example": "615601fab04c0a3147bb1246"
    },
    "account_id": {
      "type": "string",
      "example": "d199996981dbd199996981db"
    },
    "template_id": {
      "type": "string",
      "example": null,
      "nullable": true
    },
    "name": {
      "type": "string",
      "example": "document.pdf"
    },
    "status": {
      "description": "Status code — see GET /v1/documents/statuses.",
      "type": "string",
      "example": "metadata_ready"
    },
    "artifacts": {
      "description": "Artifact download URLs keyed by name (original, certificated, certificate-page, bundle).",
      "type": "object",
      "example": {
        "original": "https://api.assinafy.com.br/v1/documents/doc1/download/original"
      }
    },
    "is_closed": {
      "type": "boolean",
      "example": false
    },
    "signing_url": {
      "type": "string",
      "example": "https://api.assinafy.com.br/v1/sign/doc1"
    },
    "decline_reason": {
      "type": "string",
      "example": null,
      "nullable": true
    },
    "declined_by": {
      "oneOf": [
        {
          "$ref": "#/components/schemas/Signer"
        }
      ],
      "nullable": true
    },
    "tags": {
      "type": "array",
      "items": {
        "properties": {
          "id": {
            "type": "string"
          },
          "name": {
            "type": "string"
          }
        },
        "type": "object"
      }
    },
    "assignment": {
      "description": "Expanded assignment data when included via ?expand=assignment; null otherwise.",
      "nullable": true,
      "allOf": [
        {
          "$ref": "#/components/schemas/Assignment"
        }
      ]
    },
    "pages": {
      "type": "array",
      "items": {
        "$ref": "#/components/schemas/DocumentPage"
      }
    },
    "created_at": {
      "type": "string",
      "format": "date-time",
      "example": "2026-06-03T03:54:16Z"
    },
    "updated_at": {
      "type": "string",
      "format": "date-time",
      "example": "2026-06-03T03:54:16Z"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "resource": "document",
  "id": "615601fab04c0a3147bb1246",
  "account_id": "d199996981dbd199996981db",
  "template_id": "string",
  "name": "document.pdf",
  "status": "metadata_ready",
  "artifacts": {
    "original": "https://api.assinafy.com.br/v1/documents/doc1/download/original"
  },
  "is_closed": false,
  "signing_url": "https://api.assinafy.com.br/v1/sign/doc1",
  "decline_reason": "string",
  "declined_by": {
    "resource": "signer",
    "id": "62d6ee35c7741ca4006b9e11",
    "full_name": "John Signer",
    "email": "john@example.com",
    "whatsapp_phone_number": "+5548999990000",
    "has_accepted_terms": false
  },
  "tags": [
    {
      "id": "string",
      "name": "string"
    }
  ],
  "assignment": {
    "resource": "assignment",
    "id": "615606ef81d199996981dbce",
    "sender_email": "sender@example.com",
    "method": "virtual",
    "expires_at": "2026-08-19T12:00:00Z",
    "message": "string",
    "signers": [
      {
        "resource": "signer",
        "id": "62d6ee35c7741ca4006b9e11",
        "full_name": "John Signer",
        "email": "john@example.com",
        "whatsapp_phone_number": "+5548999990000",
        "has_accepted_terms": false,
        "verification_method": "Email",
        "notification_methods": [
          "Email"
        ],
        "step": 1,
        "notified": false,
        "completed": false,
        "notification_history": [
          {
            "event": "signature_request",
            "status": "sent",
            "error_code": "string",
            "error_message": "string",
            "sent_at": "2026-07-07T12:00:00Z",
            "failed_at": "2026-08-19T12:00:00Z"
          }
        ]
      }
    ],
    "copy_receivers": [
      {}
    ],
    "items": [
      {
        "id": "string",
        "page": {
          "id": "615601faf166d6d1d8e7dc30",
          "number": 1,
          "height": 2100,
          "width": 1275,
          "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
        },
        "signer": {},
        "field": {},
        "display_settings": {},
        "value": {},
        "completed": false
      }
    ],
    "summary": {
      "signer_count": 0,
      "completed_count": 0,
      "signers": [
        {}
      ]
    },
    "signing_urls": [
      {
        "signer_id": "string",
        "url": "https://api.assinafy.com.br/v1/sign/doc1?email=joe@example.com"
      }
    ]
  },
  "pages": [
    {
      "id": "615601faf166d6d1d8e7dc30",
      "number": 1,
      "height": 2100,
      "width": 1275,
      "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
    }
  ],
  "created_at": "2026-06-03T03:54:16Z",
  "updated_at": "2026-06-03T03:54:16Z"
}
```

### Schema: Account

Full schema:

```json
{
  "description": "A workspace account (organization).",
  "properties": {
    "resource": {
      "type": "string",
      "example": "account"
    },
    "id": {
      "type": "string",
      "example": "6401df46d6a6b0c692d9ec49"
    },
    "name": {
      "type": "string",
      "example": "Acme Inc."
    },
    "primary_color": {
      "type": "string",
      "example": "aabbcc",
      "nullable": true
    },
    "secondary_color": {
      "type": "string",
      "example": "112233",
      "nullable": true
    },
    "notification_sender_type": {
      "type": "string",
      "enum": [
        "User",
        "Account"
      ],
      "example": "User"
    },
    "roles": {
      "type": "array",
      "items": {
        "type": "string"
      },
      "example": [
        "owner"
      ]
    },
    "is_delete_allowed": {
      "type": "boolean",
      "example": true
    },
    "created_at": {
      "type": "string",
      "format": "date-time",
      "example": "2026-06-03T03:54:16Z"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "resource": "account",
  "id": "6401df46d6a6b0c692d9ec49",
  "name": "Acme Inc.",
  "primary_color": "aabbcc",
  "secondary_color": "112233",
  "notification_sender_type": "User",
  "roles": [
    "owner"
  ],
  "is_delete_allowed": true,
  "created_at": "2026-06-03T03:54:16Z"
}
```

### Schema: Field

Full schema:

```json
{
  "description": "A reusable field definition.",
  "properties": {
    "resource": {
      "type": "string",
      "example": "field"
    },
    "id": {
      "type": "string",
      "example": "6152120297080d55bdd13197"
    },
    "name": {
      "type": "string",
      "example": "Signature"
    },
    "type": {
      "type": "string",
      "example": "signature"
    },
    "regex": {
      "type": "string",
      "nullable": true
    },
    "is_pre_defined": {
      "type": "boolean"
    },
    "is_active": {
      "type": "boolean"
    },
    "is_required": {
      "type": "boolean"
    },
    "is_standard": {
      "type": "boolean"
    },
    "is_read_only": {
      "type": "boolean"
    },
    "is_visible": {
      "type": "boolean"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "resource": "field",
  "id": "6152120297080d55bdd13197",
  "name": "Signature",
  "type": "signature",
  "regex": "string",
  "is_pre_defined": false,
  "is_active": false,
  "is_required": false,
  "is_standard": false,
  "is_read_only": false,
  "is_visible": false
}
```

### Schema: Tag

Full schema:

```json
{
  "description": "A workspace-scoped label. Names are unique per workspace (case-insensitive).",
  "properties": {
    "resource": {
      "type": "string",
      "example": "tag"
    },
    "id": {
      "type": "string",
      "example": "fa8c09f3e709a8a1c82d69b1454"
    },
    "name": {
      "type": "string",
      "example": "Contracts"
    },
    "color": {
      "description": "6-char hex without leading #.",
      "type": "string",
      "example": "ff8800",
      "nullable": true
    },
    "created_at": {
      "type": "string",
      "format": "date-time",
      "example": "2026-05-14T12:00:00Z"
    },
    "updated_at": {
      "type": "string",
      "format": "date-time",
      "example": "2026-05-14T12:00:00Z"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "resource": "tag",
  "id": "fa8c09f3e709a8a1c82d69b1454",
  "name": "Contracts",
  "color": "ff8800",
  "created_at": "2026-05-14T12:00:00Z",
  "updated_at": "2026-05-14T12:00:00Z"
}
```

### Schema: SigningUrl

Full schema:

```json
{
  "properties": {
    "signer_id": {
      "type": "string"
    },
    "url": {
      "type": "string",
      "example": "https://api.assinafy.com.br/v1/sign/doc1?email=joe@example.com"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "signer_id": "string",
  "url": "https://api.assinafy.com.br/v1/sign/doc1?email=joe@example.com"
}
```

### Schema: AssignmentSigner

Full schema:

```json
{
  "description": "A signer within an assignment: the base Signer plus per-assignment verification/notification details.",
  "type": "object",
  "allOf": [
    {
      "$ref": "#/components/schemas/Signer"
    },
    {
      "properties": {
        "verification_method": {
          "type": "string",
          "example": "Email",
          "nullable": true
        },
        "notification_methods": {
          "type": "array",
          "items": {
            "type": "string"
          },
          "example": [
            "Email"
          ],
          "nullable": true
        },
        "step": {
          "description": "Sequential signing step (defaults to 1).",
          "type": "integer",
          "example": 1,
          "nullable": true
        },
        "notified": {
          "type": "boolean",
          "nullable": true
        },
        "completed": {
          "description": "Only present in account-owner contexts.",
          "type": "boolean",
          "nullable": true
        },
        "notification_history": {
          "description": "Per-channel delivery history for this signer (email + WhatsApp), most-recent send order.",
          "type": "array",
          "items": {
            "$ref": "#/components/schemas/NotificationHistoryEntry"
          },
          "nullable": true
        }
      },
      "type": "object"
    }
  ]
}
```

Example payload:

```json
{
  "resource": "signer",
  "id": "62d6ee35c7741ca4006b9e11",
  "full_name": "John Signer",
  "email": "john@example.com",
  "whatsapp_phone_number": "+5548999990000",
  "has_accepted_terms": false,
  "verification_method": "Email",
  "notification_methods": [
    "Email"
  ],
  "step": 1,
  "notified": false,
  "completed": false,
  "notification_history": [
    {
      "event": "signature_request",
      "status": "sent",
      "error_code": "string",
      "error_message": "string",
      "sent_at": "2026-07-07T12:00:00Z",
      "failed_at": "2026-08-19T12:00:00Z"
    }
  ]
}
```

### Schema: NotificationHistoryEntry

Full schema:

```json
{
  "description": "A single notification delivery record for a signer channel.",
  "properties": {
    "event": {
      "type": "string",
      "example": "signature_request"
    },
    "status": {
      "type": "string",
      "enum": [
        "sent",
        "failed"
      ],
      "example": "sent"
    },
    "error_code": {
      "type": "string",
      "nullable": true
    },
    "error_message": {
      "type": "string",
      "nullable": true
    },
    "sent_at": {
      "type": "string",
      "format": "date-time",
      "example": "2026-07-07T12:00:00Z",
      "nullable": true
    },
    "failed_at": {
      "type": "string",
      "format": "date-time",
      "example": null,
      "nullable": true
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "event": "signature_request",
  "status": "sent",
  "error_code": "string",
  "error_message": "string",
  "sent_at": "2026-07-07T12:00:00Z",
  "failed_at": "2026-08-19T12:00:00Z"
}
```

### Schema: AssignmentItem

Full schema:

```json
{
  "properties": {
    "id": {
      "type": "string"
    },
    "page": {
      "oneOf": [
        {
          "$ref": "#/components/schemas/DocumentPage"
        }
      ],
      "nullable": true
    },
    "signer": {
      "description": "Signer responsible for this item.",
      "type": "object"
    },
    "field": {
      "description": "Field definition associated with the item.",
      "type": "object",
      "nullable": true
    },
    "display_settings": {
      "description": "Rendering metadata for the item. Collect items use the DisplaySettings schema; virtual and legacy items may return an empty or non-object value."
    },
    "value": {
      "description": "Captured value when completed.",
      "nullable": true
    },
    "completed": {
      "type": "boolean"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "id": "string",
  "page": {
    "id": "615601faf166d6d1d8e7dc30",
    "number": 1,
    "height": 2100,
    "width": 1275,
    "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
  },
  "signer": {},
  "field": {},
  "display_settings": {},
  "value": {},
  "completed": false
}
```

### Schema: AssignmentSummary

Full schema:

```json
{
  "properties": {
    "signer_count": {
      "type": "integer"
    },
    "completed_count": {
      "type": "integer"
    },
    "signers": {
      "type": "array",
      "items": {
        "type": "object"
      }
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "signer_count": 0,
  "completed_count": 0,
  "signers": [
    {}
  ]
}
```

### Schema: Assignment

Full schema:

```json
{
  "description": "A request for signers to sign a document.",
  "properties": {
    "resource": {
      "type": "string",
      "example": "assignment"
    },
    "id": {
      "type": "string",
      "example": "615606ef81d199996981dbce"
    },
    "sender_email": {
      "type": "string",
      "format": "email",
      "example": "sender@example.com"
    },
    "method": {
      "type": "string",
      "enum": [
        "virtual",
        "collect"
      ],
      "example": "virtual"
    },
    "expires_at": {
      "type": "string",
      "format": "date-time",
      "example": null,
      "nullable": true
    },
    "message": {
      "type": "string",
      "nullable": true
    },
    "signers": {
      "type": "array",
      "items": {
        "$ref": "#/components/schemas/AssignmentSigner"
      }
    },
    "copy_receivers": {
      "type": "array",
      "items": {
        "type": "object"
      }
    },
    "items": {
      "type": "array",
      "items": {
        "$ref": "#/components/schemas/AssignmentItem"
      }
    },
    "summary": {
      "$ref": "#/components/schemas/AssignmentSummary"
    },
    "signing_urls": {
      "type": "array",
      "items": {
        "$ref": "#/components/schemas/SigningUrl"
      }
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "resource": "assignment",
  "id": "615606ef81d199996981dbce",
  "sender_email": "sender@example.com",
  "method": "virtual",
  "expires_at": "2026-08-19T12:00:00Z",
  "message": "string",
  "signers": [
    {
      "resource": "signer",
      "id": "62d6ee35c7741ca4006b9e11",
      "full_name": "John Signer",
      "email": "john@example.com",
      "whatsapp_phone_number": "+5548999990000",
      "has_accepted_terms": false,
      "verification_method": "Email",
      "notification_methods": [
        "Email"
      ],
      "step": 1,
      "notified": false,
      "completed": false,
      "notification_history": [
        {
          "event": "signature_request",
          "status": "sent",
          "error_code": "string",
          "error_message": "string",
          "sent_at": "2026-07-07T12:00:00Z",
          "failed_at": "2026-08-19T12:00:00Z"
        }
      ]
    }
  ],
  "copy_receivers": [
    {}
  ],
  "items": [
    {
      "id": "string",
      "page": {
        "id": "615601faf166d6d1d8e7dc30",
        "number": 1,
        "height": 2100,
        "width": 1275,
        "download_url": "https://api.assinafy.com.br/v1/documents/doc1/pages/1a/download"
      },
      "signer": {},
      "field": {},
      "display_settings": {},
      "value": {},
      "completed": false
    }
  ],
  "summary": {
    "signer_count": 0,
    "completed_count": 0,
    "signers": [
      {}
    ]
  },
  "signing_urls": [
    {
      "signer_id": "string",
      "url": "https://api.assinafy.com.br/v1/sign/doc1?email=joe@example.com"
    }
  ]
}
```

### Schema: CostEstimateBreakdownItem

Full schema:

```json
{
  "properties": {
    "code": {
      "type": "string",
      "example": "NotificationWhatsapp"
    },
    "name": {
      "type": "string",
      "example": "Whatsapp Notification"
    },
    "cost": {
      "type": "number",
      "example": 0.9
    },
    "quantity": {
      "type": "integer",
      "example": 2
    },
    "unit_cost": {
      "type": "number",
      "example": 0.45
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "code": "NotificationWhatsapp",
  "name": "Whatsapp Notification",
  "cost": 0.9,
  "quantity": 2,
  "unit_cost": 0.45
}
```

### Schema: CostEstimate

Full schema:

```json
{
  "description": "Cost breakdown for an assignment plus current account balances.",
  "properties": {
    "documents": {
      "description": "Documents consumed (always 1).",
      "type": "integer",
      "example": 1
    },
    "credits": {
      "description": "Total notification credits needed.",
      "type": "number"
    },
    "needs_extra_document": {
      "description": "True when the plan's document allowance is exhausted and an extra document will be charged from credits.",
      "type": "boolean"
    },
    "extra_document_cost": {
      "description": "Credits charged for the extra document when `needs_extra_document` is true.",
      "type": "number",
      "example": 1
    },
    "total_credits": {
      "type": "number"
    },
    "breakdown": {
      "type": "array",
      "items": {
        "$ref": "#/components/schemas/CostEstimateBreakdownItem"
      }
    },
    "document_balance": {
      "type": "number"
    },
    "credit_balance": {
      "type": "number"
    },
    "has_sufficient_resources": {
      "type": "boolean"
    },
    "blocking_reason": {
      "type": "string",
      "enum": [
        "PendingPayment",
        "InsufficientDocuments",
        "InsufficientCredits"
      ],
      "example": null,
      "nullable": true
    },
    "message": {
      "type": "string",
      "nullable": true
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "documents": 1,
  "credits": 0,
  "needs_extra_document": false,
  "extra_document_cost": 1,
  "total_credits": 0,
  "breakdown": [
    {
      "code": "NotificationWhatsapp",
      "name": "Whatsapp Notification",
      "cost": 0.9,
      "quantity": 2,
      "unit_cost": 0.45
    }
  ],
  "document_balance": 0,
  "credit_balance": 0,
  "has_sufficient_resources": false,
  "blocking_reason": "PendingPayment",
  "message": "string"
}
```

### Schema: TemplateFieldPlacement

Full schema:

```json
{
  "properties": {
    "id": {
      "type": "string"
    },
    "field_id": {
      "type": "string"
    },
    "role_id": {
      "type": "string"
    },
    "label": {
      "type": "string"
    },
    "display_settings": {
      "description": "Rendering metadata for the placement."
    },
    "created_at": {
      "type": "string",
      "format": "date-time"
    },
    "updated_at": {
      "type": "string",
      "format": "date-time"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "id": "string",
  "field_id": "string",
  "role_id": "string",
  "label": "string",
  "display_settings": {},
  "created_at": "2026-08-19T12:00:00Z",
  "updated_at": "2026-08-19T12:00:00Z"
}
```

### Schema: TemplatePage

Full schema:

```json
{
  "properties": {
    "id": {
      "type": "string"
    },
    "number": {
      "type": "integer",
      "example": 1
    },
    "height": {
      "type": "integer",
      "example": 2100
    },
    "width": {
      "type": "integer",
      "example": 1275
    },
    "download_url": {
      "type": "string"
    },
    "fields": {
      "type": "array",
      "items": {
        "$ref": "#/components/schemas/TemplateFieldPlacement"
      }
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "id": "string",
  "number": 1,
  "height": 2100,
  "width": 1275,
  "download_url": "string",
  "fields": [
    {
      "id": "string",
      "field_id": "string",
      "role_id": "string",
      "label": "string",
      "display_settings": {},
      "created_at": "2026-08-19T12:00:00Z",
      "updated_at": "2026-08-19T12:00:00Z"
    }
  ]
}
```

### Schema: TemplateRole

Full schema:

```json
{
  "properties": {
    "id": {
      "type": "string"
    },
    "name": {
      "type": "string",
      "example": "Editor"
    },
    "assignment_type": {
      "type": "string",
      "example": "Editor"
    },
    "created_at": {
      "type": "string",
      "format": "date-time"
    },
    "updated_at": {
      "type": "string",
      "format": "date-time"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "id": "string",
  "name": "Editor",
  "assignment_type": "Editor",
  "created_at": "2026-08-19T12:00:00Z",
  "updated_at": "2026-08-19T12:00:00Z"
}
```

### Schema: Template

Full schema:

```json
{
  "description": "A reusable document template.",
  "properties": {
    "resource": {
      "type": "string",
      "example": "template"
    },
    "id": {
      "type": "string",
      "example": "fa88b732db84d01427d4cdd1092"
    },
    "name": {
      "type": "string",
      "example": "template.pdf"
    },
    "document_name": {
      "description": "Default name for documents created from this template.",
      "type": "string",
      "nullable": true
    },
    "message": {
      "description": "Default invitation message.",
      "type": "string",
      "nullable": true
    },
    "status": {
      "description": "One of uploading, uploaded, processing, ready, failed.",
      "type": "string",
      "example": "ready"
    },
    "pages": {
      "type": "array",
      "items": {
        "$ref": "#/components/schemas/TemplatePage"
      }
    },
    "roles": {
      "type": "array",
      "items": {
        "$ref": "#/components/schemas/TemplateRole"
      }
    },
    "tags": {
      "type": "array",
      "items": {
        "properties": {
          "id": {
            "type": "string"
          },
          "name": {
            "type": "string"
          }
        },
        "type": "object"
      }
    },
    "default_document_tags": {
      "description": "Applied to documents created from this template; only returned by the single-template endpoint.",
      "type": "array",
      "items": {
        "properties": {
          "id": {
            "type": "string"
          },
          "name": {
            "type": "string"
          }
        },
        "type": "object"
      }
    },
    "created_at": {
      "type": "string",
      "format": "date-time"
    },
    "updated_at": {
      "type": "string",
      "format": "date-time"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "resource": "template",
  "id": "fa88b732db84d01427d4cdd1092",
  "name": "template.pdf",
  "document_name": "string",
  "message": "string",
  "status": "ready",
  "pages": [
    {
      "id": "string",
      "number": 1,
      "height": 2100,
      "width": 1275,
      "download_url": "string",
      "fields": [
        {
          "id": "string",
          "field_id": "string",
          "role_id": "string",
          "label": "string",
          "display_settings": {},
          "created_at": "2026-08-19T12:00:00Z",
          "updated_at": "2026-08-19T12:00:00Z"
        }
      ]
    }
  ],
  "roles": [
    {
      "id": "string",
      "name": "Editor",
      "assignment_type": "Editor",
      "created_at": "2026-08-19T12:00:00Z",
      "updated_at": "2026-08-19T12:00:00Z"
    }
  ],
  "tags": [
    {
      "id": "string",
      "name": "string"
    }
  ],
  "default_document_tags": [
    {
      "id": "string",
      "name": "string"
    }
  ],
  "created_at": "2026-08-19T12:00:00Z",
  "updated_at": "2026-08-19T12:00:00Z"
}
```

### Schema: WebhookSubscription

Full schema:

```json
{
  "description": "An account's webhook subscription configuration.",
  "properties": {
    "events": {
      "description": "Event types subscribed for delivery.",
      "type": "array",
      "items": {
        "type": "string"
      },
      "example": [
        "document_ready",
        "document_prepared"
      ]
    },
    "is_active": {
      "description": "Whether webhook delivery is active.",
      "type": "boolean",
      "example": true
    },
    "url": {
      "description": "Webhook endpoint URL.",
      "type": "string",
      "example": "http://example.com?test=1",
      "nullable": true
    },
    "email": {
      "description": "Contact email for delivery notices.",
      "type": "string",
      "example": "email@example.com",
      "nullable": true
    },
    "updated_at": {
      "type": "string",
      "format": "date-time",
      "example": "2023-05-10T14:58:24Z",
      "nullable": true
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "events": [
    "document_ready",
    "document_prepared"
  ],
  "is_active": true,
  "url": "http://example.com?test=1",
  "email": "email@example.com",
  "updated_at": "2023-05-10T14:58:24Z"
}
```

### Schema: WebhookDispatch

Full schema:

```json
{
  "description": "A single webhook delivery-history entry.",
  "properties": {
    "resource": {
      "description": "Always `activity_dispatching_history` in single-resource responses.",
      "type": "string",
      "example": "activity_dispatching_history"
    },
    "id": {
      "description": "Dispatch entry ID.",
      "type": "string",
      "example": "a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6"
    },
    "event": {
      "description": "Event type that triggered the dispatch.",
      "type": "string",
      "example": "document_ready"
    },
    "activity_id": {
      "description": "Internal activity ID associated with the dispatch.",
      "type": "integer",
      "example": 456
    },
    "endpoint": {
      "description": "URL that received the request.",
      "type": "string",
      "example": "https://example.com/webhook",
      "nullable": true
    },
    "payload": {
      "description": "JSON payload sent to the endpoint.",
      "type": "object",
      "nullable": true
    },
    "delivered": {
      "description": "Whether delivery succeeded.",
      "type": "boolean",
      "example": true
    },
    "http_status": {
      "description": "HTTP status returned (null if connection failed).",
      "type": "integer",
      "example": 200,
      "nullable": true
    },
    "response_body": {
      "description": "Endpoint response body, truncated to 2000 chars.",
      "type": "string",
      "example": "OK",
      "nullable": true
    },
    "error": {
      "description": "Delivery error message, if any.",
      "type": "string",
      "example": null,
      "nullable": true
    },
    "created_at": {
      "type": "string",
      "format": "date-time",
      "example": "2024-01-15T10:30:00Z"
    },
    "updated_at": {
      "type": "string",
      "format": "date-time",
      "example": "2024-01-15T10:30:00Z"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "resource": "activity_dispatching_history",
  "id": "a1b2c3d4e5f6g7h8i9j0k1l2m3n4o5p6",
  "event": "document_ready",
  "activity_id": 456,
  "endpoint": "https://example.com/webhook",
  "payload": {},
  "delivered": true,
  "http_status": 200,
  "response_body": "OK",
  "error": "string",
  "created_at": "2024-01-15T10:30:00Z",
  "updated_at": "2024-01-15T10:30:00Z"
}
```

### Schema: WebhookEventType

Full schema:

```json
{
  "description": "A subscribable webhook event type.",
  "properties": {
    "id": {
      "description": "Event type code.",
      "type": "string",
      "example": "document_ready"
    },
    "description": {
      "description": "When the event is triggered.",
      "type": "string",
      "example": "Triggered when the last Signer of the assignment signs the Document."
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "id": "document_ready"
}
```

### Schema: AccountTheme

Full schema:

```json
{
  "description": "An account's branding theme.",
  "properties": {
    "account_name": {
      "type": "string",
      "example": "Account Name"
    },
    "primary_color": {
      "description": "Hex color without leading `#`.",
      "type": "string",
      "example": "aabbcc"
    },
    "secondary_color": {
      "type": "string",
      "example": "aabbcc",
      "nullable": true
    },
    "logo": {
      "description": "URL to the account logo.",
      "type": "string",
      "example": "https://api.assinafy.com.br/v1/accounts/1a/logo"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "account_name": "Account Name",
  "primary_color": "aabbcc",
  "secondary_color": "aabbcc",
  "logo": "https://api.assinafy.com.br/v1/accounts/1a/logo"
}
```

### Schema: FieldType

Full schema:

```json
{
  "description": "A supported field/validation type.",
  "properties": {
    "type": {
      "type": "string",
      "example": "cpf"
    },
    "name": {
      "type": "string",
      "example": "CPF"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "type": "cpf",
  "name": "CPF"
}
```

### Schema: FieldValidation

Full schema:

```json
{
  "description": "The result of validating a value against a field definition.",
  "properties": {
    "type": {
      "description": "The field's validation type.",
      "type": "string",
      "example": "cpf"
    },
    "success": {
      "type": "boolean",
      "example": true
    },
    "error_message": {
      "description": "Empty when valid.",
      "type": "string",
      "example": ""
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "type": "cpf",
  "success": true,
  "error_message": ""
}
```

### Schema: FieldValidationResult

Full schema:

```json
{
  "description": "A per-field result from a multi-field validation.",
  "properties": {
    "field_id": {
      "type": "string",
      "example": "63488ffb7adf435aba319787"
    },
    "type": {
      "type": "string",
      "example": "cpf"
    },
    "success": {
      "type": "boolean",
      "example": false
    },
    "error_message": {
      "type": "string",
      "example": "Invalid CPF."
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "field_id": "63488ffb7adf435aba319787",
  "type": "cpf",
  "success": false,
  "error_message": "Invalid CPF."
}
```

### Schema: DocumentVerification

Full schema:

```json
{
  "description": "The verification result for a document looked up by signature hash. When not verified, most fields are null and `is_valid` is false.",
  "properties": {
    "hash": {
      "type": "string",
      "example": "FE32EDDADE7CBDDCBB934E7402047450B0E59C02"
    },
    "id": {
      "type": "string",
      "example": "63ddb172402799bfc991d10d",
      "nullable": true
    },
    "status": {
      "type": "string",
      "example": "certificated",
      "nullable": true
    },
    "page_count": {
      "type": "string",
      "example": "1",
      "nullable": true
    },
    "signer_count": {
      "type": "string",
      "example": "1",
      "nullable": true
    },
    "completed_count": {
      "type": "integer",
      "example": 1,
      "nullable": true
    },
    "completed_at": {
      "type": "string",
      "format": "date-time",
      "example": "2023-01-27T19:27:44Z",
      "nullable": true
    },
    "verified_at": {
      "type": "string",
      "format": "date-time",
      "example": "2023-01-27T19:27:46Z"
    },
    "is_valid": {
      "type": "boolean",
      "example": true
    },
    "message": {
      "description": "Reason when not valid.",
      "type": "string",
      "example": ""
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "hash": "FE32EDDADE7CBDDCBB934E7402047450B0E59C02",
  "id": "63ddb172402799bfc991d10d",
  "status": "certificated",
  "page_count": "1",
  "signer_count": "1",
  "completed_count": 1,
  "completed_at": "2023-01-27T19:27:44Z",
  "verified_at": "2023-01-27T19:27:46Z",
  "is_valid": true,
  "message": ""
}
```

### Schema: DocumentActivity

Full schema:

```json
{
  "description": "A document activity/audit event.",
  "properties": {
    "id": {
      "type": "integer",
      "example": 4
    },
    "event": {
      "description": "Event type code.",
      "type": "string",
      "example": "assignment_created"
    },
    "message": {
      "type": "string",
      "example": "Assignment created by John Smith."
    },
    "payload": {
      "description": "Event-specific payload snapshot. Keys vary per event.",
      "type": "object",
      "nullable": true
    },
    "origin": {
      "description": "Request origin when available.",
      "properties": {
        "ip": {
          "type": "string",
          "example": "172.19.0.1"
        },
        "user-agent": {
          "type": "string"
        }
      },
      "type": "object",
      "nullable": true
    },
    "created_at": {
      "type": "string",
      "format": "date-time",
      "example": "2022-07-19T19:28:13Z"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "id": 4,
  "event": "assignment_created",
  "message": "Assignment created by John Smith.",
  "payload": {},
  "origin": {
    "ip": "172.19.0.1",
    "user-agent": "string"
  },
  "created_at": "2022-07-19T19:28:13Z"
}
```

### Schema: WhatsappNotification

Full schema:

```json
{
  "description": "A rendered WhatsApp notification sent for an assignment, split into header/body/buttons as the signer would see them.",
  "properties": {
    "sent_at": {
      "description": "Unix timestamp when sent.",
      "type": "integer",
      "example": 1710000000
    },
    "header": {
      "type": "string",
      "example": "Documento para assinatura: Contrato de Servico"
    },
    "body": {
      "type": "string"
    },
    "buttons": {
      "type": "array",
      "items": {
        "properties": {
          "text": {
            "description": "The button label shown to the signer.",
            "type": "string",
            "example": "Abrir documento"
          }
        },
        "type": "object"
      }
    },
    "phone_number": {
      "description": "Recipient phone (E.164).",
      "type": "string",
      "example": "+5511999990001"
    },
    "signer_id": {
      "type": "string",
      "example": "a51edaee68a7"
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "sent_at": 1710000000,
  "header": "Documento para assinatura: Contrato de Servico",
  "body": "string",
  "buttons": [
    {
      "text": "Abrir documento"
    }
  ],
  "phone_number": "+5511999990001",
  "signer_id": "a51edaee68a7"
}
```

### Schema: AuthSession

Full schema:

```json
{
  "description": "A JWT access token plus the authenticated user and the accounts they belong to.",
  "properties": {
    "access_token": {
      "type": "string",
      "example": "example-access-token"
    },
    "user": {
      "$ref": "#/components/schemas/AuthUser"
    },
    "accounts": {
      "type": "array",
      "items": {
        "$ref": "#/components/schemas/AuthAccount"
      }
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "access_token": "example-access-token",
  "user": {
    "id": "bgjazeo5r9v2lq7l36dx48np",
    "name": "John Smith",
    "email": "example@example.com",
    "telephone": "17989206641",
    "government_id": "15774136604",
    "is_email_verified": false,
    "has_accepted_terms": true,
    "created_at": "2023-03-03T11:51:34Z",
    "to_be_deleted_at": "2026-08-19T12:00:00Z"
  },
  "accounts": [
    {
      "id": "6401df46d6a6b0c692d9ec49",
      "name": "JS",
      "roles": [
        "owner"
      ],
      "is_delete_allowed": true,
      "created_at": "2023-03-03T11:51:34Z"
    }
  ]
}
```

### Schema: DocumentStatsRow

Full schema:

```json
{
  "description": "One period of the document-funnel KPI series. `period` is `YYYY-MM` (monthly) or `YYYY-MM-DD` (daily); series are zero-filled, no gaps. Signature requests come with two independent breakdowns: the `signature_requests_notification_*` counters split them by the channels the signer was notified on — a signer reached on more than one channel counts once per channel, so these add up to at least `signature_requests` — while the `signature_requests_verification_*` counters split them by how the signer's identity is verified, and since each request has exactly one verification method those four always add up to `signature_requests`.",
  "properties": {
    "period": {
      "description": "`YYYY-MM` (monthly) or `YYYY-MM-DD` (daily).",
      "type": "string",
      "example": "2026-06"
    },
    "documents_uploaded": {
      "type": "integer",
      "example": 42
    },
    "documents_sent": {
      "type": "integer",
      "example": 37
    },
    "signature_requests": {
      "type": "integer",
      "example": 61
    },
    "signature_requests_notification_email": {
      "description": "Requests notified by e-mail.",
      "type": "integer",
      "example": 55
    },
    "signature_requests_notification_whatsapp": {
      "description": "Requests notified by WhatsApp.",
      "type": "integer",
      "example": 18
    },
    "signature_requests_notification_bypass": {
      "description": "Requests with no notification sent (`Bypass`).",
      "type": "integer",
      "example": 3
    },
    "signature_requests_verification_email": {
      "description": "Requests verified by an e-mail token.",
      "type": "integer",
      "example": 48
    },
    "signature_requests_verification_whatsapp": {
      "description": "Requests verified by a WhatsApp token.",
      "type": "integer",
      "example": 6
    },
    "signature_requests_verification_bypass": {
      "description": "Requests signed without token verification (`Bypass`).",
      "type": "integer",
      "example": 3
    },
    "signature_requests_verification_digital_certificate": {
      "description": "Requests signed with the signer's own ICP-Brasil digital certificate.",
      "type": "integer",
      "example": 4
    },
    "signature_requests_viewed": {
      "description": "Signature requests whose document was first viewed during the period.",
      "type": "integer",
      "example": 44
    },
    "signature_requests_completed": {
      "description": "Signature requests completed by individual signers during the period.",
      "type": "integer",
      "example": 52
    },
    "documents_certified": {
      "type": "integer",
      "example": 30
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "period": "2026-06",
  "documents_uploaded": 42,
  "documents_sent": 37,
  "signature_requests": 61,
  "signature_requests_notification_email": 55,
  "signature_requests_notification_whatsapp": 18,
  "signature_requests_notification_bypass": 3,
  "signature_requests_verification_email": 48,
  "signature_requests_verification_whatsapp": 6,
  "signature_requests_verification_bypass": 3,
  "signature_requests_verification_digital_certificate": 4,
  "signature_requests_viewed": 44,
  "signature_requests_completed": 52,
  "documents_certified": 30
}
```

### Schema: NotificationPreferences

Full schema:

```json
{
  "description": "Owner-facing document notifications, keyed by notification type. `true` means the e-mail is sent.",
  "properties": {
    "DocumentCompleted": {
      "description": "Every signer has signed and the document is certified.",
      "type": "boolean",
      "example": true
    },
    "SignerDeclined": {
      "description": "A signer declined to sign.",
      "type": "boolean",
      "example": true
    },
    "DocumentCancelled": {
      "description": "The document was cancelled.",
      "type": "boolean",
      "example": true
    },
    "DocumentAboutToExpire": {
      "description": "The signature deadline is approaching.",
      "type": "boolean",
      "example": true
    },
    "DocumentExpired": {
      "description": "The signature deadline passed.",
      "type": "boolean",
      "example": true
    },
    "DocumentExpirationReset": {
      "description": "The signature deadline was extended.",
      "type": "boolean",
      "example": true
    },
    "DocumentProcessingFailed": {
      "description": "An uploaded document could not be processed.",
      "type": "boolean",
      "example": true
    },
    "TemplateProcessingFailed": {
      "description": "A template could not be processed.",
      "type": "boolean",
      "example": true
    },
    "SignerWhatsappFailed": {
      "description": "A WhatsApp notification to a signer could not be delivered.",
      "type": "boolean",
      "example": true
    }
  },
  "type": "object"
}
```

Example payload:

```json
{
  "DocumentCompleted": true,
  "SignerDeclined": true,
  "DocumentCancelled": true,
  "DocumentAboutToExpire": true,
  "DocumentExpired": true,
  "DocumentExpirationReset": true,
  "DocumentProcessingFailed": true,
  "TemplateProcessingFailed": true,
  "SignerWhatsappFailed": true
}
```
