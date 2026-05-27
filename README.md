# Assinafy .NET SDK

.NET SDK for the [Assinafy API](https://api.assinafy.com.br/v1/docs).

The SDK follows the documented API surface 1:1: authentication, documents,
signers, signer-facing signing flows, assignments, fields, templates, tags,
public document token delivery, signature images, and webhooks. Every
endpoint was verified against `https://api.assinafy.com.br/v1` during
the 1.1.0 audit (see CHANGELOG).

## Requirements

- .NET 8 SDK or later (`net8.0`, `net9.0`, and `net10.0` are supported)

## Installation

```bash
dotnet add package Assinafy.Sdk
```

## Quick Start

```csharp
using Assinafy.Sdk;
using Assinafy.Sdk.Models;

using var client = new AssinafyClient(new AssinafyClientOptions
{
    ApiKey = Environment.GetEnvironmentVariable("ASSINAFY_API_KEY"),
    AccountId = Environment.GetEnvironmentVariable("ASSINAFY_ACCOUNT_ID"),
});

await using var stream = File.OpenRead("contract.pdf");
var document = await client.Documents.UploadAsync(stream, "contract.pdf");

var signer = await client.Signers.CreateAsync(new CreateSignerRequest
{
    FullName = "John Doe",
    Email = "john@example.com",
    WhatsAppPhoneNumber = "+5548999990000",
});

var assignment = await client.Assignments.CreateAsync(document.Id, new CreateAssignmentRequest
{
    Method = "virtual",
    Signers = [signer.Id],
    Message = "Please review and sign",
});
```

## Dependency Injection

```csharp
using Assinafy.Sdk;

builder.Services.AddAssinafy(o =>
{
    o.ApiKey = builder.Configuration["Assinafy:ApiKey"];
    o.AccountId = builder.Configuration["Assinafy:AccountId"];
});
```

`AddAssinafy` returns the underlying `IHttpClientBuilder` so you can chain
Polly handlers, custom message handlers, etc.

## Authentication

```csharp
var auth = await client.Authentication.LoginAsync(new LoginRequest
{
    Email = "user@example.com",
    Password = "password",
});

var key = await client.Authentication.CreateApiKeyAsync(new CreateApiKeyRequest
{
    Password = "password",
});
```

API-key and bearer-token clients are both supported:

```csharp
new AssinafyClientOptions { ApiKey = "api-key", AccountId = "account-id" };
new AssinafyClientOptions { Token = "access-token", AccountId = "account-id" };
```

## Resources

Documents:

```csharp
await client.Documents.ListStatusesAsync();
await client.Documents.ListAsync(new Dictionary<string, string?> { ["sort"] = "-updated_at" });
await client.Documents.GetAsync(documentId);
await client.Documents.ActivitiesAsync(documentId);
await client.Documents.DownloadAsync(documentId, DocumentArtifactNames.Certificated);
await client.Documents.ThumbnailAsync(documentId);
await client.Documents.DownloadPageAsync(documentId, pageId);
await client.Documents.VerifyAsync(signatureHash);
await client.Documents.DeleteAsync(documentId);
```

After an upload, use `WaitUntilReadyAsync` to poll until the document
reaches `metadata_ready`, `pending_signature`, or `certificated` (and
throws if the document fails or expires):

```csharp
await client.Documents.WaitUntilReadyAsync(documentId);
```

Templates:

```csharp
var templates = await client.Templates.ListAsync();
var template = await client.Templates.GetAsync(templateId);

await client.Documents.CreateFromTemplateAsync(
    templateId,
    [new TemplateSigner { RoleId = template.Roles[0].Id, Id = signerId }]);
```

Tags:

```csharp
// Workspace tags
var tag = await client.Tags.CreateAsync(new CreateTagRequest { Name = "Contracts", Color = "3366FF" });
await client.Tags.ListAsync(search: "contr");
await client.Tags.UpdateAsync(tag.Id, new UpdateTagRequest { Name = "Signed contracts" });
await client.Tags.DeleteAsync(tag.Id, force: true); // force detaches from documents/templates first

// Document tags (referenced by name; created on the fly if new)
await client.Tags.AddToDocumentAsync(documentId, ["Contracts"]);   // append
await client.Tags.SetForDocumentAsync(documentId, ["Contracts"]);  // replace all (pass [] to clear)
await client.Tags.ListForDocumentAsync(documentId);
await client.Tags.RemoveFromDocumentAsync(documentId, tag.Id);     // detach one
```

Assignments:

```csharp
await client.Assignments.EstimateCostAsync(documentId, new CreateAssignmentRequest
{
    Signers = [new SignerRef { VerificationMethod = "Whatsapp" }],
});

await client.Assignments.ResetExpirationAsync(documentId, assignmentId, null);
await client.Assignments.ResendNotificationAsync(documentId, assignmentId, signerId);
await client.Assignments.EstimateResendCostAsync(documentId, assignmentId, signerId);
await client.Assignments.ListWhatsAppNotificationsAsync(documentId, assignmentId);
```

Signer-facing flow:

```csharp
await client.Signers.GetSelfAsync(signerAccessCode);
await client.Signers.AcceptTermsAsync(signerAccessCode);
await client.Signers.VerifyEmailAsync(signerAccessCode, "123456");
await client.Signers.ConfirmDataAsync(documentId, signerAccessCode, new ConfirmSignerDataRequest
{
    Email = "john@example.com",
    HasAcceptedTerms = true,
});

await client.Signing.GetAsync(signerAccessCode);
await client.Signing.SignAsync(documentId, assignmentId, signerAccessCode, values);
await client.Signing.SignMultipleAsync(signerAccessCode, [documentId]);
await client.Signing.DeclineAsync(documentId, assignmentId, signerAccessCode, "Unfavorable terms.");
```

Fields:

```csharp
await client.Fields.CreateAsync(new CreateFieldDefinitionRequest { Type = "text", Name = "Field Name" });
await client.Fields.ListAsync(new FieldListParams { IncludeStandard = true });
await client.Fields.ValidateAsync(fieldId, new ValidateFieldValueRequest { Value = "ABC" });
await client.Fields.ListTypesAsync();
```

Public documents and signature images:

```csharp
await client.PublicDocuments.GetAsync(documentId);
await client.PublicDocuments.SendTokenAsync(documentId, new SendDocumentTokenRequest
{
    Recipient = "john@example.com",
    Channel = "email",
});

await client.Signatures.UploadAsync(signaturePngStream, signerAccessCode);
await client.Signatures.DownloadAsync(signerAccessCode, SignatureImageTypes.Signature);
```

Webhooks:

```csharp
await client.Webhooks.UpdateSubscriptionAsync(new UpdateWebhookSubscriptionRequest
{
    Url = "https://example.com/webhooks/assinafy",
    Email = "ops@example.com",
    IsActive = true,
    Events = ["document_ready", "signer_signed_document"],
});

await client.Webhooks.GetAsync();
await client.Webhooks.InactivateAsync();
await client.Webhooks.DeleteAsync();
await client.Webhooks.ListEventTypesAsync();
await client.Webhooks.ListDispatchesAsync(new ListDispatchesParams { Delivered = false });
await client.Webhooks.RetryDispatchAsync(dispatchId);
```

## Error Handling

The SDK normalises every response and surfaces three exception types under
the common `AssinafyException` base:

- `ValidationException` — invalid input caught before the request leaves
  the SDK (missing IDs, non-PDF uploads, oversized files, etc.).
- `ApiException` — the API returned a non-2xx status or an envelope with
  `status >= 400`. `StatusCode` and `ApiMessage` carry the upstream details.
- `NetworkException` — transport failures and timeouts.

```csharp
try
{
    await client.Documents.GetAsync("missing");
}
catch (ApiException ex) when (ex.StatusCode == 404)
{
    // not found
}
```

## Running Tests

```bash
dotnet test Assinafy.Sdk.sln
```

The suite (73 tests) covers every resource on `net8.0`, `net9.0`, and
`net10.0`. For live API verification against the production endpoint, see
the live-test script described in the CHANGELOG.
