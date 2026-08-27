using System.Text;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests;

/// <summary>
/// End-to-end tests that exercise the Assinafy sandbox. Configuration is read from
/// environment variables, and the tests fail before sending a request if the base URL is not
/// the sandbox. The two test-email variables are optional and default to reserved example.com
/// addresses. Run with, e.g.:
/// <code>
/// ASSINAFY_API_KEY=... ASSINAFY_ACCOUNT_ID=... ASSINAFY_BASE_URL=https://sandbox.assinafy.com.br/v1 \
/// ASSINAFY_TEST_EMAIL_PRIMARY=... ASSINAFY_TEST_EMAIL_SECONDARY=... \
///   dotnet test --project tests/Assinafy.Sdk.Tests/Assinafy.Sdk.Tests.csproj \
///     --framework net10.0 -- --filter-trait "Category=Live"
/// </code>
/// </summary>
public sealed class LiveIntegrationTests
{
    private const string SandboxBaseUrl = "https://sandbox.assinafy.com.br/v1";
    private static readonly SemaphoreSlim SandboxRequestGate = new(1, 1);
    private static DateTimeOffset _nextSandboxRequestAt;

    private static AssinafyClient CreateClient(string? accountId = null)
    {
        var baseUrl = RequiredEnvironmentVariable("ASSINAFY_BASE_URL").TrimEnd('/');
        if (!string.Equals(baseUrl, SandboxBaseUrl, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Live tests only run against {SandboxBaseUrl}.");

        var http = new HttpClient(new SandboxThrottleHandler());
        return new AssinafyClient(new AssinafyClientOptions
        {
            ApiKey = RequiredEnvironmentVariable("ASSINAFY_API_KEY"),
            AccountId = accountId ?? RequiredEnvironmentVariable("ASSINAFY_ACCOUNT_ID"),
            BaseUrl = baseUrl,
        }, http, ownsHttpClient: true);
    }

    private static string RequiredEnvironmentVariable(string name) =>
        Environment.GetEnvironmentVariable(name) is { } value && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidOperationException($"{name} must be configured to run live tests.");

    private static string TestEmail(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) is { } value && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : fallback;

    private sealed class SandboxThrottleHandler : DelegatingHandler
    {
        private static readonly TimeSpan RequestInterval = TimeSpan.FromSeconds(6);

        public SandboxThrottleHandler()
            : base(AssinafyClient.CreatePrimaryHandler()) { }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await SandboxRequestGate.WaitAsync(cancellationToken);
            try
            {
                var delay = _nextSandboxRequestAt - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, cancellationToken);

                _nextSandboxRequestAt = DateTimeOffset.UtcNow + RequestInterval;
            }
            finally
            {
                SandboxRequestGate.Release();
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }

    private static async Task RetryRateLimitedCleanupAsync(Func<Task> cleanup)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await cleanup();
                return;
            }
            catch (ApiException exception) when (exception.StatusCode == 429 && attempt < 2)
            {
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
        }
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task ReadEndpoints_AuthenticateAndReturnData()
    {
        var client = CreateClient();

        using (client)
        {
            (await client.Documents.ListStatusesAsync()).Should().NotBeEmpty();
            (await client.Fields.ListTypesAsync()).Should().NotBeEmpty();
            (await client.Webhooks.ListEventTypesAsync()).Should().NotBeEmpty();

            var documents = await client.Documents.ListAsync(
                new Dictionary<string, string?> { ["per-page"] = "1" });
            documents.Should().NotBeNull();

            await client.Tags.ListAsync();
            await client.Fields.ListAsync();
        }
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task UploadWaitGetDelete_RoundTrips()
    {
        var client = CreateClient();
        var suffix = Guid.NewGuid().ToString("N");

        using (client)
        {
            using var pdf = new MemoryStream(BuildMinimalPdf());
            var uploaded = await client.Documents.UploadAsync(pdf, $"sdk-live-{suffix}.pdf");
            uploaded.Id.Should().NotBeNullOrEmpty();

            try
            {
                var ready = await client.Documents.WaitUntilReadyAsync(uploaded.Id);
                ready.Id.Should().Be(uploaded.Id);

                var fetched = await client.Documents.GetAsync(uploaded.Id);
                fetched.Id.Should().Be(uploaded.Id);

                // PATCH /documents/{id} — rename is allowed before any assignment exists.
                var renamed = await client.Documents.RenameAsync(uploaded.Id, $"sdk-live-renamed-{suffix}");
                renamed.Id.Should().Be(uploaded.Id);
                renamed.Name.Should().NotBeNullOrEmpty();

                (await client.Documents.ActivitiesAsync(uploaded.Id)).Should().NotBeEmpty();
            }
            finally
            {
                await RetryRateLimitedCleanupAsync(() => client.Documents.DeleteAsync(uploaded.Id));
            }
        }
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task TemplateLifecycle_RoundTrips()
    {
        using var client = CreateClient();
        using var pdf = new MemoryStream(BuildMinimalPdf());
        var suffix = Guid.NewGuid().ToString("N");
        TemplateDetails? template = null;

        try
        {
            template = await client.Templates.CreateAsync(
                pdf,
                $"sdk-live-{suffix}.pdf",
                $"sdk-live-{suffix}");
            template.Id.Should().NotBeNullOrWhiteSpace();

            var deadline = DateTimeOffset.UtcNow.AddMinutes(2);
            while (template.Pages.Count == 0 && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
                template = await client.Templates.GetAsync(template.Id);
            }

            template.Pages.Should().NotBeEmpty();
            var pageId = template.Pages[0].Id;
            template = await client.Templates.UpdateAsync(
                template.Id,
                new UpdateTemplateRequest
                {
                    Name = $"sdk-live-updated-{suffix}",
                    Message = "Sandbox lifecycle test",
                });
            template.Name.Should().Contain("updated");
            (await client.Templates.DownloadPageAsync(template.Id, pageId))
                .Should().NotBeEmpty();
        }
        finally
        {
            if (template is not null && !string.IsNullOrWhiteSpace(template.Id))
                await RetryRateLimitedCleanupAsync(() => client.Templates.DeleteAsync(template.Id));
        }
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task NewEndpoints_AccountsAssignmentsAndSearch_Work()
    {
        var client = CreateClient();

        using (client)
        {
            // Accounts resource (added in 1.3.0).
            var accounts = await client.Accounts.ListAsync();
            accounts.Should().NotBeEmpty();

            var account = await client.Accounts.GetAsync();
            account.Id.Should().NotBeNullOrEmpty();

            var theme = await client.Accounts.GetThemeAsync();
            theme.AccountName.Should().NotBeNull();

            // GET /assignments (account context sent via the accountId query parameter).
            var assignments = await client.Assignments.ListAsync(
                new AssignmentListParams { PerPage = 5 },
                account.Id);
            assignments.Should().NotBeNull();

            // Compact document-search route.
            var search = await client.Documents.SearchAsync(perPage: 5);
            search.Should().NotBeNull();

            _ = await client.Signers.FindByEmailAsync(
                TestEmail("ASSINAFY_TEST_EMAIL_PRIMARY", "assinafy-sdk-primary@example.com"));
            _ = await client.Signers.FindByEmailAsync(
                TestEmail("ASSINAFY_TEST_EMAIL_SECONDARY", "assinafy-sdk-secondary@example.com"));
        }
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task DisposableAccountLifecycle_CoversAuthenticatedEndpointsAndCleansUp()
    {
        var primaryEmail = TestEmail("ASSINAFY_TEST_EMAIL_PRIMARY", "assinafy-sdk-primary@example.com");
        var secondaryEmail = TestEmail("ASSINAFY_TEST_EMAIL_SECONDARY", "assinafy-sdk-secondary@example.com");
        primaryEmail.Should().NotBeEquivalentTo(secondaryEmail);

        using var bootstrap = CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        Account? account = null;

        try
        {
            account = await bootstrap.Accounts.CreateAsync(new CreateAccountRequest
            {
                Name = $"sdk-live-{suffix}",
            });
            account.Id.Should().NotBeNullOrWhiteSpace();

            using var client = CreateClient(account.Id);

            (await client.Accounts.GetAsync()).Id.Should().Be(account.Id);
            (await client.Accounts.UpdateAsync(new UpdateAccountRequest
            {
                Name = $"sdk-live-updated-{suffix}",
            })).Id.Should().Be(account.Id);
            (await client.Accounts.GetThemeAsync()).AccountName.Should().NotBeNullOrWhiteSpace();

            using (var logo = new MemoryStream(Convert.FromBase64String(
                       "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=")))
            {
                await client.Accounts.UploadLogoAsync(logo, "sdk-live.png");
            }
            (await client.Accounts.DownloadLogoAsync()).Should().NotBeEmpty();
            await client.Accounts.DeleteLogoAsync();

            var primarySigner = await client.Signers.CreateAsync(new CreateSignerRequest
            {
                FullName = $"SDK Primary {suffix}",
                Email = primaryEmail,
            });
            var secondarySigner = await client.Signers.CreateAsync(new CreateSignerRequest
            {
                FullName = $"SDK Secondary {suffix}",
                Email = secondaryEmail,
            });

            (await client.Signers.GetAsync(primarySigner.Id)).Email.Should().Be(primaryEmail);
            (await client.Signers.UpdateAsync(primarySigner.Id, new UpdateSignerRequest
            {
                FullName = $"SDK Primary Updated {suffix}",
            })).FullName.Should().Contain("Updated");
            (await client.Signers.ListAsync(new Dictionary<string, string?>
            {
                ["search"] = primaryEmail,
            })).Data.Should().Contain(signer => signer.Id == primarySigner.Id);
            (await client.Signers.FindByEmailAsync(secondaryEmail))?.Id.Should().Be(secondarySigner.Id);

            using var pdf = new MemoryStream(BuildMinimalPdf());
            var document = await client.Documents.UploadAsync(pdf, $"sdk-live-{suffix}.pdf");
            var ready = await client.Documents.WaitUntilReadyAsync(
                document.Id,
                maxWait: TimeSpan.FromMinutes(2));
            ready.Id.Should().Be(document.Id);
            ready.Pages.Should().NotBeEmpty();

            (await client.Documents.GetAsync(document.Id)).Id.Should().Be(document.Id);
            (await client.Documents.ListAsync(new Dictionary<string, string?>
            {
                ["search"] = suffix,
            })).Data.Should().Contain(item => item.Id == document.Id);

            (await client.Documents.RenameAsync(document.Id, $"sdk-live-renamed-{suffix}"))
                .Id.Should().Be(document.Id);
            (await client.Documents.SearchAsync(search: suffix)).Data
                .Should().Contain(item => item.Id == document.Id);
            (await client.Documents.DownloadAsync(document.Id, DocumentArtifactNames.Original))
                .Should().NotBeEmpty();
            (await client.Documents.ThumbnailAsync(document.Id)).Should().NotBeEmpty();
            (await client.Documents.DownloadPageAsync(document.Id, ready.Pages[0].Id))
                .Should().NotBeEmpty();
            (await client.Documents.ActivitiesAsync(document.Id)).Should().NotBeEmpty();

            var tagName = $"sdk-live-{suffix}";
            var tag = await client.Tags.CreateAsync(new CreateTagRequest
            {
                Name = tagName,
                Color = "224466",
            });
            tag = await client.Tags.UpdateAsync(tag.Id, new UpdateTagRequest
            {
                Name = $"{tagName}-updated",
                ClearColor = true,
            });
            tag.Color.Should().BeNull();
            (await client.Tags.ListAsync(search: suffix)).Should().Contain(item => item.Id == tag.Id);
            // Sandbox interprets this array as names; production OpenAPI documents tag IDs.
            (await client.Tags.AddToDocumentAsync(document.Id, [tag.Name]))
                .Should().Contain(item => item.Id == tag.Id);
            (await client.Tags.ListForDocumentAsync(document.Id))
                .Should().Contain(item => item.Id == tag.Id);
            (await client.Tags.RemoveFromDocumentWithResultAsync(document.Id, tag.Id))
                .Detached.Should().BeTrue();
            (await client.Tags.DeleteWithResultAsync(tag.Id)).Deleted.Should().BeTrue();

            var fieldTypes = await client.Fields.ListTypesAsync();
            fieldTypes.Should().Contain(type => type.Type == "text");
            var field = await client.Fields.CreateAsync(new CreateFieldDefinitionRequest
            {
                Type = "text",
                Name = $"SDK Live {suffix}",
                IsRequired = true,
            });
            (await client.Fields.GetAsync(field.Id)).Id.Should().Be(field.Id);
            field = await client.Fields.UpdateAsync(field.Id, new UpdateFieldDefinitionRequest
            {
                Name = $"SDK Live Updated {suffix}",
                ClearRegex = true,
            });
            field.Regex.Should().BeNull();
            (await client.Fields.ListAsync()).Data.Should().Contain(item => item.Id == field.Id);
            (await client.Fields.ValidateAsync(field.Id, new ValidateFieldValueRequest
            {
                Value = "sdk-live",
            })).Success.Should().BeTrue();
            (await client.Fields.ValidateMultipleAsync(
            [
                new ValidateFieldValueItem { FieldId = field.Id, Value = "sdk-live" },
            ])).Should().ContainSingle(result => result.Success);
            await RetryRateLimitedCleanupAsync(() => client.Fields.DeleteAsync(field.Id));

            var estimate = await client.Assignments.EstimateCostAsync(document.Id, new CreateAssignmentRequest
            {
                Method = AssignmentMethods.Virtual,
                Signers =
                [
                    new SignerRef
                    {
                        VerificationMethod = SignerChannels.Email,
                        NotificationMethods = [SignerChannels.Email],
                    },
                ],
            });
            estimate.TotalCredits.Should().BeGreaterThanOrEqualTo(0);
            (await client.Assignments.ListAsync(
                new AssignmentListParams { PerPage = 5 },
                account.Id))
                .Should().NotBeNull();

            var eventTypes = await client.Webhooks.ListEventTypesAsync();
            var eventType = eventTypes.First(type => !string.IsNullOrWhiteSpace(type.Id));
            (await client.Webhooks.GetAsync()).Should().NotBeNull();
            var subscription = await client.Webhooks.UpdateSubscriptionAsync(
                new UpdateWebhookSubscriptionRequest
                {
                    Events = [eventType.Id],
                    IsActive = true,
                    Url = "https://example.invalid/assinafy-sdk-live-test",
                    Email = secondaryEmail,
                });
            subscription.IsActive.Should().BeTrue();
            (await client.Webhooks.GetAsync()).Events.Should().Contain(eventType.Id);
            (await client.Webhooks.ListDispatchesAsync(new ListDispatchesParams { PerPage = 5 }))
                .Should().NotBeNull();
            (await client.Webhooks.InactivateAsync()).IsActive.Should().BeFalse();

            await RetryRateLimitedCleanupAsync(() => client.Documents.DeleteAsync(document.Id));
            await RetryRateLimitedCleanupAsync(() => client.Signers.DeleteAsync(primarySigner.Id));
            await RetryRateLimitedCleanupAsync(() => client.Signers.DeleteAsync(secondarySigner.Id));
        }
        finally
        {
            if (account is not null && !string.IsNullOrWhiteSpace(account.Id))
                await RetryRateLimitedCleanupAsync(
                    () => bootstrap.Accounts.DeleteAsync(force: true, accountId: account.Id));
        }
    }

    /// <summary>Builds a tiny but valid single-page PDF the API will accept.</summary>
    private static byte[] BuildMinimalPdf()
    {
        var stream = "BT /F1 24 Tf 72 700 Td (Assinafy SDK live test) Tj ET";
        var objects = new[]
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {stream.Length} >>\nstream\n{stream}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };

        using var ms = new MemoryStream();
        void Write(string text) => ms.Write(Encoding.ASCII.GetBytes(text));

        Write("%PDF-1.4\n");
        var offsets = new long[objects.Length];
        for (var i = 0; i < objects.Length; i++)
        {
            offsets[i] = ms.Length;
            Write($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = ms.Length;
        Write($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            Write($"{offset:D10} 00000 n \n");
        Write($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");

        return ms.ToArray();
    }
}
