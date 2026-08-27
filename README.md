# Assinafy .NET SDK

Typed .NET client for the [Assinafy API](https://api.assinafy.com.br/v1/docs), targeting `net8.0`, `net9.0`, and `net10.0`.

- [Complete SDK and HTTP API reference](docs/API.md) — every public resource method plus all 89 production operations, parameters, request/response schemas, examples, authentication, and errors.
- [Checked-in production OpenAPI snapshot](docs/openapi.json)
- [Security policy](SECURITY.md)
- [MIT license](LICENSE)

## Requirements

- Applications: a runtime compatible with `net8.0`, `net9.0`, or `net10.0`.
- Contributors: .NET SDKs 8.0.424, 9.0.317, and 10.0.400; [`global.json`](global.json) selects .NET 10 for repository commands.

## Installation

```bash
dotnet add package Assinafy.Sdk --version 1.3.2
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

// Deliver assignment.SigningUrls through your application or let Assinafy notify each signer.
// After the human signing flow finishes, download the certified artifact.
using var completionDeadline = new CancellationTokenSource(TimeSpan.FromHours(1));
DocumentDetails completed;
do
{
    await Task.Delay(TimeSpan.FromSeconds(2), completionDeadline.Token);
    completed = await client.Documents.GetAsync(document.Id, completionDeadline.Token);
}
while (!string.Equals(completed.Status, "certificated", StringComparison.OrdinalIgnoreCase));

var certifiedPdf = await client.Documents.DownloadAsync(document.Id);
await File.WriteAllBytesAsync("contract-signed.pdf", certifiedPdf);
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

The SDK accepts only an absolute HTTPS base URL whose path is exactly `/v1`; user info, custom paths, query strings, and fragments are rejected. SDK-owned and dependency-injected transports disable automatic redirects so `X-Api-Key` cannot be forwarded to a redirect target. If you supply an `HttpClient`, its base address must exactly match `BaseUrl`, and its primary handler must also disable redirects:

```csharp
using var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
using var http = new HttpClient(handler)
{
    BaseAddress = new Uri("https://sandbox.assinafy.com.br/v1/"),
};
using var client = new AssinafyClient(new AssinafyClientOptions
{
    ApiKey = Environment.GetEnvironmentVariable("ASSINAFY_API_KEY"),
    AccountId = Environment.GetEnvironmentVariable("ASSINAFY_ACCOUNT_ID"),
    BaseUrl = "https://sandbox.assinafy.com.br/v1",
}, http);
```

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

foreach (var row in accountStats)
{
    Console.WriteLine($"{row.Period}: {row.SignatureRequests} requests");
    Console.WriteLine($"Email notifications: {row.SignatureRequestsNotificationEmail}");
    Console.WriteLine($"Certificate verifications: {row.SignatureRequestsVerificationDigitalCertificate}");
}

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
await using var templatePdf = File.OpenRead("template.pdf");
var uploadedTemplate = await client.Templates.CreateAsync(
    templatePdf,
    "template.pdf",
    "Sales agreement");
using var templateReadyDeadline = new CancellationTokenSource(TimeSpan.FromMinutes(2));
while (uploadedTemplate.Pages.Count == 0)
{
    await Task.Delay(TimeSpan.FromSeconds(2), templateReadyDeadline.Token);
    uploadedTemplate = await client.Templates.GetAsync(
        uploadedTemplate.Id,
        cancellationToken: templateReadyDeadline.Token);
}
var templatePageId = uploadedTemplate.Pages[0].Id;
uploadedTemplate = await client.Templates.UpdateAsync(
    uploadedTemplate.Id,
    new UpdateTemplateRequest { Message = "Please review and sign" });
var templatePage = await client.Templates.DownloadPageAsync(
    uploadedTemplate.Id,
    templatePageId);
var estimate = await client.Documents.EstimateCostFromTemplateAsync(
    templateId,
    [new TemplateSigner { RoleId = template.Roles[0].Id }]);
```

Delete templates that are no longer needed with `client.Templates.DeleteAsync(templateId)`.

### Assignments and signer flow

```csharp
var cost = await client.Assignments.EstimateCostAsync(documentId, new CreateAssignmentRequest
{
    Method = AssignmentMethods.Virtual,
    Signers = [new SignerRef { Id = signerId, VerificationMethod = SignerChannels.Email }],
});

await client.Signers.AcceptTermsAsync(signerAccessCode);
await client.Signers.VerifyAsync(signerAccessCode, verificationCode);
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

For a production assignment whose verification method is `DigitalCertificate`, start the Web PKI operation, sign the returned token in the signer’s browser, then complete it:

```csharp
var operation = await client.Signing.StartCertificateAsync(signerAccessCode);
var signedToken = await SignWithWebPkiAsync(operation.Token); // your browser/Web PKI bridge
var certificateResult = await client.Signing.CompleteCertificateAsync(
    signerAccessCode,
    signedToken);
Console.WriteLine(certificateResult.SignerName);
```

The certificate start and completion routes are production-only deployed extensions. They are not exposed by the sandbox or included in the published OpenAPI document, and require a valid production certificate assignment and browser-signed Web PKI token.

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

All SDK-specific exceptions derive from `AssinafyException`:

- `ValidationException` — invalid input rejected before transport.
- `ApiException` — an HTTP or API-envelope error. Inspect `StatusCode`, `ApiMessage`, and structured `Details`.
- `NetworkException` — transport failure or timeout.
- `SerializationException` — a request body could not be serialized or a successful response did not match the expected envelope/payload.

Standard .NET argument, cancellation, disposal, and stream exceptions retain their platform types.

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

The regular suite runs with xUnit v3 on Microsoft.Testing.Platform against stubbed HTTP transport on all supported target frameworks and excludes live tests. Arguments after `--` are test-runner options:

```bash
dotnet test --solution Assinafy.Sdk.sln -- --filter-not-trait "Category=Live"
```

Live tests are sandbox-only and fail fast if a required credential is missing or if the URL is not exactly the sandbox base URL:

```bash
ASSINAFY_API_KEY=... \
ASSINAFY_ACCOUNT_ID=... \
ASSINAFY_BASE_URL=https://sandbox.assinafy.com.br/v1 \
dotnet test --project tests/Assinafy.Sdk.Tests/Assinafy.Sdk.Tests.csproj \
  --framework net10.0 -- --filter-trait "Category=Live"
```

`ASSINAFY_TEST_EMAIL_PRIMARY` and `ASSINAFY_TEST_EMAIL_SECONDARY` are optional overrides. Without them, the suite uses reserved `example.com` addresses. The GitHub `sandbox` environment therefore needs only `ASSINAFY_API_KEY` and `ASSINAFY_ACCOUNT_ID` secrets.

The sandbox suite does not call the production-only certificate start and completion routes. Local transport tests cover request construction, credential isolation, and success-envelope deserialization; the complete flow requires a valid production certificate assignment and browser-signed Web PKI token.
