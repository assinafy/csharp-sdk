# Assinafy .NET SDK

Typed .NET client for the [Assinafy API](https://api.assinafy.com.br/v1/docs), targeting `net8.0`, `net9.0`, and `net10.0`.

- [Complete SDK and HTTP API reference](docs/API.md) — every public resource method plus all 89 production operations, parameters, request/response schemas, examples, authentication, and errors.
- [Checked-in production OpenAPI snapshot](docs/openapi.json)
- [Security policy](SECURITY.md)
- [MIT license](LICENSE)

## Requirements

- Applications: a runtime compatible with `net8.0`, `net9.0`, or `net10.0`.
- Contributors: the .NET 10 LTS SDK selected by [`global.json`](global.json).

## Installation

After the package's first trusted-published NuGet release:

```bash
dotnet add package Assinafy.Sdk
```

Until that release is visible on NuGet.org, reference the source project:

```xml
<ProjectReference Include="path/to/csharp-sdk/src/Assinafy.Sdk/Assinafy.Sdk.csproj" />
```

## Quick start

```csharp
using Assinafy.Sdk;
using Assinafy.Sdk.Models;

using var client = new AssinafyClient(new AssinafyClientOptions
{
    ApiKey = Environment.GetEnvironmentVariable("ASSINAFY_API_KEY"),
    AccountId = Environment.GetEnvironmentVariable("ASSINAFY_ACCOUNT_ID"),
});

await using var pdf = File.OpenRead("contract.pdf");
var document = await client.Documents.UploadAsync(pdf, "contract.pdf");
await client.Documents.WaitUntilReadyAsync(document.Id);

var signer = await client.Signers.CreateAsync(new CreateSignerRequest
{
    FullName = "John Doe",
    Email = "john@example.com",
});

var assignment = await client.Assignments.CreateAsync(document.Id, new CreateAssignmentRequest
{
    Method = AssignmentMethods.Virtual,
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
```

The default base URL is production. Set sandbox explicitly when testing:

```csharp
var client = new AssinafyClient(new AssinafyClientOptions
{
    ApiKey = Environment.GetEnvironmentVariable("ASSINAFY_API_KEY"),
    AccountId = Environment.GetEnvironmentVariable("ASSINAFY_ACCOUNT_ID"),
    BaseUrl = "https://sandbox.assinafy.com.br/v1",
});
```

Never commit API keys. Reuse one `AssinafyClient` for the lifetime of the application, or register it through dependency injection.

## Dependency injection

```csharp
builder.Services.AddAssinafy(options =>
{
    options.ApiKey = builder.Configuration["Assinafy:ApiKey"];
    options.AccountId = builder.Configuration["Assinafy:AccountId"];
});
```

`AddAssinafy` returns `IHttpClientBuilder`, so standard handlers and resilience policies can be chained.

## Common operations

### Accounts and users

```csharp
var accounts = await client.Accounts.ListAsync();
var account = await client.Accounts.GetAsync();
var theme = await client.Accounts.GetThemeAsync();
var accountStats = await client.Accounts.GetStatsAsync(new DocumentStatsParams
{
    Granularity = DocumentStatsGranularities.Monthly,
});

var user = await client.Users.GetSelfAsync();
var preferences = await client.Users.GetNotificationPreferencesAsync();
await client.Users.UpdateNotificationPreferencesAsync(new UpdateNotificationPreferencesRequest
{
    DocumentCompleted = true,
});
```

### Documents and templates

```csharp
var documents = await client.Documents.ListAsync();
var details = await client.Documents.GetAsync(documentId);
var activities = await client.Documents.ActivitiesAsync(documentId);
var original = await client.Documents.DownloadAsync(documentId, DocumentArtifactNames.Original);
var pades = await client.Documents.DownloadAsync(documentId, DocumentArtifactNames.Pades);

var templates = await client.Templates.ListAsync();
var template = await client.Templates.GetAsync(templateId);
var estimate = await client.Documents.EstimateCostFromTemplateAsync(
    templateId,
    [new TemplateSigner { RoleId = template.Roles[0].Id }]);
```

`TemplateResource.GetAsync` is retained because the route works in the sandbox and the API schema refers to it, although the production OpenAPI path list currently omits it.

### Assignments and signer flow

```csharp
var cost = await client.Assignments.EstimateCostAsync(documentId, new CreateAssignmentRequest
{
    Method = AssignmentMethods.Virtual,
    Signers = [new SignerRef { Id = signerId, VerificationMethod = SignerChannels.Email }],
});

await client.Signers.AcceptTermsAsync(signerAccessCode);
await client.Signers.VerifyEmailAsync(signerAccessCode, verificationCode);
var confirmed = await client.Signers.ConfirmDataWithResultAsync(
    documentId,
    signerAccessCode,
    new ConfirmSignerDataRequest
    {
        FullName = "John Doe",
        Email = "john@example.com",
        GovernmentId = "00000000000",
    });

await client.Signing.SignAsync(documentId, assignmentId, signerAccessCode, values);
```

Signer-facing and public requests deliberately do not receive the client's API key or bearer token. They use only the documented signer access code or no authentication.

### Public documents and signatures

```csharp
var publicDocument = await client.PublicDocuments.GetDetailsAsync(documentId);
await client.PublicDocuments.SendTokenAsync(documentId, "john@example.com");

await client.Signatures.UploadAsync(
    signaturePngStream,
    signerAccessCode,
    reuse: true,
    type: SignatureImageTypes.Signature);
```

### Tags, fields, and webhooks

```csharp
var tag = await client.Tags.CreateAsync(new CreateTagRequest
{
    Name = "Contracts",
    Color = "3366FF",
});
await client.Tags.AddToDocumentAsync(documentId, [tag.Id]);
var detached = await client.Tags.RemoveFromDocumentWithResultAsync(documentId, tag.Id);

var field = await client.Fields.CreateAsync(new CreateFieldDefinitionRequest
{
    Name = "Customer reference",
    Type = "text",
});

await client.Webhooks.UpdateSubscriptionAsync(new UpdateWebhookSubscriptionRequest
{
    Url = "https://example.com/webhooks/assinafy",
    Email = "ops@example.com",
    IsActive = true,
    Events = ["document_ready"],
});
```

### One-call convenience flow

```csharp
await using var pdf = File.OpenRead("contract.pdf");
var result = await client.UploadAndRequestSignaturesAsync(new UploadAndRequestSignaturesOptions
{
    FileStream = pdf,
    FileName = "contract.pdf",
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
```

This helper is intentionally not transactional because the API has no transaction spanning upload, signer creation, and assignment creation. If a later request fails, previously created resources remain available for inspection or cleanup.

## Error handling

All SDK exceptions derive from `AssinafyException`:

- `ValidationException` — invalid input rejected before transport.
- `ApiException` — an HTTP or API-envelope error. Inspect `StatusCode`, `ApiMessage`, and structured `Details`.
- `NetworkException` — transport failure or timeout.
- `SerializationException` — a successful response did not match the expected envelope/payload.

```csharp
try
{
    await client.Documents.GetAsync("missing");
}
catch (ApiException ex) when (ex.StatusCode == 404)
{
    Console.WriteLine(ex.ApiMessage);
    Console.WriteLine(ex.Details?.GetRawText());
}
```

## Tests

The regular suite runs against stubbed HTTP transport on all supported target frameworks and excludes live tests:

```bash
dotnet test Assinafy.Sdk.sln --filter "Category!=Live"
```

Live tests are sandbox-only and fail fast if any required setting is missing or if the URL is not exactly the sandbox base URL:

```bash
ASSINAFY_API_KEY=... \
ASSINAFY_ACCOUNT_ID=... \
ASSINAFY_BASE_URL=https://sandbox.assinafy.com.br/v1 \
ASSINAFY_TEST_EMAIL_PRIMARY=first@example.com \
ASSINAFY_TEST_EMAIL_SECONDARY=second@example.com \
dotnet test Assinafy.Sdk.sln --filter "FullyQualifiedName~LiveIntegrationTests"
```

The production OpenAPI currently contains account/user stats and notification-preference operations that the sandbox returns as `404`; see the compatibility section in [the API reference](docs/API.md). Those routes are contract-tested locally and must be live-tested when Assinafy brings the sandbox to parity.
