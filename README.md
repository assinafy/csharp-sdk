# Assinafy .NET SDK

.NET 8 SDK for the [Assinafy API](https://api.assinafy.com.br/v1/docs).

The SDK follows the documented API surface: authentication, documents, signers,
signer-facing signing flows, assignments, fields, templates, public document
token delivery, signature images, and webhooks.

## Requirements

- .NET 8 SDK or later

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

`AddAssinafy` returns the underlying `IHttpClientBuilder`.

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

Templates:

```csharp
var templates = await client.Templates.ListAsync();
var template = await client.Templates.GetAsync(templateId);

await client.Documents.CreateFromTemplateAsync(
    templateId,
    [new TemplateSigner { RoleId = template.Roles[0].Id, Id = signerId }]);
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

## Running Tests

```bash
dotnet test Assinafy.Sdk.sln
```
