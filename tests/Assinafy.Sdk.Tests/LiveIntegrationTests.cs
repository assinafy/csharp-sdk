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

    private static string RequiredEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(value),
            $"{name} is not configured; set the sandbox live-test environment variables to run this test.");

        return value!.Trim();
    }

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
            (await client.Fields.ValidateAsWorkspaceAsync(field.Id, new ValidateFieldValueRequest
            {
                Value = "sdk-live",
            })).Success.Should().BeTrue();
            (await client.Fields.ValidateMultipleAsWorkspaceAsync(
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
            eventTypes.Should().Contain(type => !string.IsNullOrWhiteSpace(type.Id));
            (await client.Webhooks.GetAsync()).Should().NotBeNull();
            (await client.Webhooks.ListDispatchesAsync(new ListDispatchesParams { PerPage = 5 }))
                .Should().NotBeNull();

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

    [Fact]
    [Trait("Category", "Live")]
    public async Task AssignmentExpirationSignerDocumentsAndSendToken_FollowTheContract()
    {
        using var client = CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"signer-{suffix[..12]}@example.com";
        Signer? signer = null;
        DocumentDetails? document = null;

        try
        {
            signer = await client.Signers.CreateAsync(new CreateSignerRequest
            {
                FullName = $"SDK Contract {suffix[..8]}",
                Email = email,
            });
            using var pdf = new MemoryStream(BuildMinimalPdf());
            document = await client.Documents.UploadAsync(pdf, $"sdk-contract-{suffix}.pdf");
            await client.Documents.WaitUntilReadyAsync(document.Id, maxWait: TimeSpan.FromMinutes(2));

            var assignment = await client.Assignments.CreateAsync(document.Id, new CreateAssignmentRequest
            {
                Method = AssignmentMethods.Virtual,
                Signers = [new SignerRef { Id = signer.Id }],
            });

            var expiresAt = DateTimeOffset.UtcNow.AddDays(3).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
            (await client.Assignments.ResetExpirationAsync(document.Id, assignment.Id, expiresAt))
                .ExpiresAt.Should().NotBeNullOrWhiteSpace();
            (await client.Assignments.ResetExpirationAsync(document.Id, assignment.Id, null))
                .ExpiresAt.Should().BeNull();

            (await client.Signing.ListDocumentsAsWorkspaceAsync(signer.Id)).Data
                .Should().Contain(item => item.Id == document.Id);
            (await client.Signing.SearchDocumentsAsWorkspaceAsync(signer.Id)).Data
                .Should().Contain(item => item.Id == document.Id);

            var sent = await client.PublicDocuments.SendTokenAsync(document.Id, new SendDocumentTokenRequest
            {
                Channel = SendDocumentTokenChannels.Email,
                Recipient = email,
            });
            sent.Channel.Should().Be(SendDocumentTokenChannels.Email);
            sent.Recipient.Should().Be(email);
            sent.Document!.Id.Should().Be(document.Id);
        }
        finally
        {
            try
            {
                if (document is not null)
                    await RetryRateLimitedCleanupAsync(() => client.Documents.DeleteAsync(document.Id));
            }
            finally
            {
                if (signer is not null)
                    await RetryRateLimitedCleanupAsync(() => client.Signers.DeleteAsync(signer.Id));
            }
        }
    }


    [Fact]
    [Trait("Category", "Live")]
    public async Task WebhookEndpointLifecycle_RotatesTestsAndDeletesConfiguredReceiver()
    {
        var receiverUrl = RequiredEnvironmentVariable("ASSINAFY_TEST_WEBHOOK_URL");
        if (!Uri.TryCreate(receiverUrl, UriKind.Absolute, out var receiver) || receiver.Scheme != Uri.UriSchemeHttps)
            throw new InvalidOperationException("ASSINAFY_TEST_WEBHOOK_URL must be an authorized public HTTPS receiver.");

        using var bootstrap = CreateClient();
        Account? account = null;
        try
        {
            account = await bootstrap.Accounts.CreateAsync(new CreateAccountRequest
            {
                Name = $"sdk-webhook-{Guid.NewGuid():N}",
            });
            using var client = CreateClient(account.Id);
            var endpoint = await client.Webhooks.CreateEndpointAsync(new CreateWebhookEndpointRequest
            {
                Url = receiverUrl,
                Email = TestEmail("ASSINAFY_TEST_EMAIL_SECONDARY", "assinafy-sdk-secondary@example.com"),
                Events = ["document_ready"],
                SigningEnabled = true,
                IsActive = false,
            });
            endpoint.Secret.Should().NotBeNullOrWhiteSpace();
            (await client.Webhooks.GetEndpointAsync(endpoint.Id)).IsActive.Should().BeFalse();
            (await client.Webhooks.ListEndpointsByUrlAsync(receiverUrl)).Should().ContainSingle();

            var rotated = await client.Webhooks.RotateEndpointSecretWithOptionsAsync(endpoint.Id, keepPrevious: true);
            (rotated.Secret != endpoint.Secret).Should().BeTrue("rotation must replace the secret");
            rotated.PreviousSecretExpiresAt.Should().NotBeNullOrWhiteSpace();

            var dispatch = await client.Webhooks.TestEndpointAsync(endpoint.Id);
            dispatch.Event.Should().Be("webhook_test");
            dispatch.Kind.Should().Be("manual");
            dispatch.Delivered.Should().BeTrue();
            dispatch.WillRetry.Should().BeFalse();
            dispatch.DurationMs.Should().BeGreaterThanOrEqualTo(0);
            (await client.Webhooks.GetEndpointAsync(endpoint.Id)).IsActive.Should().BeFalse();
            (await client.Webhooks.ListDispatchesAsync(new ListDispatchesParams { EndpointId = endpoint.Id }))
                .Data.Should().Contain(item => item.Id == dispatch.Id);

            await client.Webhooks.DeleteEndpointByUrlAsync(receiverUrl);
            (await client.Webhooks.ListEndpointsByUrlAsync(receiverUrl)).Should().BeEmpty();
        }
        finally
        {
            if (account is not null)
                await RetryRateLimitedCleanupAsync(() => bootstrap.Accounts.DeleteAsync(force: true, accountId: account.Id));
        }
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task ContractLifecycle_PreservesTermsConcurrencyAndPendingAmendments()
    {
        using var client = CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        DocumentDetails? document = null;
        DocumentDetails? instrument = null;
        try
        {
            using var pdf = new MemoryStream(BuildMinimalPdf());
            document = await client.Documents.UploadAsync(pdf, $"sdk-contract-{suffix}.pdf");
            await client.Documents.WaitUntilReadyAsync(document.Id, maxWait: TimeSpan.FromMinutes(2));
            var contract = (await client.Contracts.SetAsync(document.Id, new Dictionary<string, object?>
            {
                ["status"] = "active",
                ["counterparty_name"] = $"SDK {suffix}",
                ["value"] = 123.45m,
                ["start_date"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd"),
                ["end_date"] = DateTimeOffset.UtcNow.AddYears(1).ToString("yyyy-MM-dd"),
                ["external_references"] = new[] { new { system = "sdk", id = suffix } },
            })).Should().BeOfType<DocumentContract>().Subject;
            contract.Value.Should().Be(123.45m);
            contract.Currency.Should().Be("BRL");
            var staleUpdatedAt = DateTimeOffset.Parse(contract.UpdatedAt!, System.Globalization.CultureInfo.InvariantCulture)
                .AddSeconds(-1).ToString("O", System.Globalization.CultureInfo.InvariantCulture);
            contract = (DocumentContract)await client.Contracts.SetAsync(document.Id, new Dictionary<string, object?>
            {
                ["counterparty_name"] = $"SDK changed {suffix}",
                ["value"] = null,
                ["updated_at"] = contract.UpdatedAt,
            });
            contract.Value.Should().BeNull();
            var staleWrite = () => client.Contracts.SetAsync(document.Id, new Dictionary<string, object?>
            {
                ["value"] = 999m,
                ["updated_at"] = staleUpdatedAt,
            });
            (await staleWrite.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(409);
            ((DocumentContract)await client.Contracts.GetAsync(document.Id)).Value.Should().BeNull();
            (await client.Contracts.ListAsync(new Dictionary<string, string?>
            {
                ["external_system"] = "sdk",
                ["external_id"] = suffix,
            })).Data.Should().ContainSingle(item => item.DocumentId == document.Id);

            var previousTerm = contract.Term;
            contract = await client.Contracts.RenewAsync(document.Id, new Dictionary<string, object?>
            {
                ["term"] = previousTerm,
                ["updated_at"] = contract.UpdatedAt,
            });
            contract.Term.Should().Be(previousTerm + 1);
            var repeatRenewal = () => client.Contracts.RenewAsync(document.Id,
                new Dictionary<string, object?> { ["term"] = previousTerm });
            (await repeatRenewal.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(400);

            using var instrumentPdf = new MemoryStream(BuildMinimalPdf());
            instrument = await client.Documents.UploadAsync(instrumentPdf, $"sdk-amendment-{suffix}.pdf");
            await client.Documents.WaitUntilReadyAsync(instrument.Id, maxWait: TimeSpan.FromMinutes(2));
            var amendment = (await client.Contracts.SetAsync(instrument.Id, new Dictionary<string, object?>
            {
                ["parent_document_id"] = document.Id,
                ["relation_type"] = "amendment",
                ["value"] = 456.78m,
            })).Should().BeOfType<DocumentContractAmendment>().Subject;
            amendment.Status.Should().Be("pending");
            (await client.Contracts.GetRelatedAsync(instrument.Id)).Amendments
                .Should().ContainSingle(item => item.DocumentId == instrument.Id);
            amendment = (DocumentContractAmendment)await client.Contracts.SetAsync(instrument.Id,
                new Dictionary<string, object?> { ["value"] = null, ["updated_at"] = amendment.UpdatedAt });
            (amendment.Terms?.ContainsKey("value") == true).Should().BeFalse();
            await client.Contracts.SetAsync(instrument.Id,
                new Dictionary<string, object?> { ["parent_document_id"] = null, ["updated_at"] = amendment.UpdatedAt });
            (await client.Contracts.GetRelatedAsync(document.Id)).Amendments.Should().BeEmpty();

            contract = (DocumentContract)await client.Contracts.GetAsync(document.Id);
            contract = await client.Contracts.TerminateAsync(document.Id, new Dictionary<string, object?>
            {
                ["reason"] = "SDK lifecycle completed",
                ["updated_at"] = contract.UpdatedAt,
            });
            contract.Status.Should().Be("terminated");
            var renewTerminated = () => client.Contracts.RenewAsync(document.Id,
                new Dictionary<string, object?> { ["term"] = contract.Term });
            (await renewTerminated.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(400);
        }
        finally
        {
            try
            {
                if (instrument is not null)
                    await RetryRateLimitedCleanupAsync(() => client.Documents.DeleteAsync(instrument.Id));
            }
            finally
            {
                if (document is not null)
                    await RetryRateLimitedCleanupAsync(() => client.Documents.DeleteAsync(document.Id));
            }
        }
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task EnvelopeLifecycle_RoundTrips()
    {
        using var client = CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        var email = TestEmail("ASSINAFY_TEST_EMAIL_PRIMARY", $"sdk-envelope-{suffix}@example.com");
        var documents = new List<DocumentDetails>();
        Signer? signer = null;
        var createdSigner = false;
        DocumentEnvelope? envelope = null;
        try
        {
            signer = await client.Signers.FindByEmailAsync(email);
            if (signer is null)
            {
                signer = await client.Signers.CreateAsync(new CreateSignerRequest
                {
                    FullName = $"SDK Envelope {suffix[..8]}",
                    Email = email,
                });
                createdSigner = true;
            }
            var signerId = signer.Id;
            for (var index = 0; index < 3; index++)
            {
                using var pdf = new MemoryStream(BuildMinimalPdf());
                var document = await client.Documents.UploadAsync(pdf, $"sdk-envelope-{suffix}-{index}.pdf");
                documents.Add(document);
                await client.Documents.WaitUntilReadyAsync(document.Id, maxWait: TimeSpan.FromMinutes(2));
            }
            EnvelopeDocumentRequest Member(DocumentDetails document) => new()
            {
                Id = document.Id,
                SignerIds = [signerId],
                Method = AssignmentMethods.Virtual,
            };
            var request = new CreateEnvelopeRequest
            {
                Name = $"SDK envelope {suffix}",
                Message = "Please sign",
                Signers = [new EnvelopeSignerRequest
                {
                    Id = signerId,
                    VerificationMethod = SignerChannels.Email,
                    NotificationMethods = [SignerChannels.Email],
                }],
                Documents = [Member(documents[0]), Member(documents[1])],
                MaxTotalCredits = 0,
            };
            var estimate = await client.Envelopes.EstimateCostAsync(request);
            estimate.TotalCredits.Should().Be(0, "this lifecycle only permits free operations");
            estimate.HasSufficientResources.Should().BeTrue();

            var idempotencyKey = $"sdk-envelope-{suffix}";
            envelope = await client.Envelopes.CreateAsync(request, idempotencyKey);
            envelope.Documents.Select(item => item.Id).Should().Equal(documents.Take(2).Select(item => item.Id));
            (await client.Envelopes.CreateAsync(request, idempotencyKey)).Id.Should().Be(envelope.Id);
            (await client.Envelopes.ListAsync(new Dictionary<string, string?> { ["search"] = request.Name }))
                .Data.Should().Contain(item => item.Id == envelope.Id);
            (await client.Envelopes.GetAsync(envelope.Id)).Documents.Should().HaveCount(2);
            (await client.Envelopes.ActivitiesAsync(envelope.Id)).Data.Should().NotBeNull();
            var zip = await client.Envelopes.DownloadAsync(envelope.Id, DocumentArtifactNames.Original);
            using (var zipStream = new MemoryStream(zip))
            using (var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read))
                archive.Entries.Where(entry => !entry.FullName.EndsWith('/')).Should().HaveCount(2);

            var reordered = new[] { documents[1].Id, documents[0].Id };
            envelope = await client.Envelopes.UpdateAsync(envelope.Id, new Dictionary<string, object?>
            {
                ["name"] = $"SDK updated {suffix}",
                ["message"] = null,
                ["document_ids"] = reordered,
            });
            envelope.Name.Should().Be($"SDK updated {suffix}");
            envelope.Message.Should().BeNull();
            envelope.Documents.Select(item => item.Id).Should().Equal(reordered);
            envelope = await client.Envelopes.AddDocumentAsync(envelope.Id, Member(documents[2]), maxTotalCredits: 0);
            envelope.Documents.Select(item => item.Id).Should().Equal(reordered.Append(documents[2].Id));
            envelope = await client.Envelopes.RemoveDocumentAsync(envelope.Id, documents[2].Id);
            envelope.Documents.Select(item => item.Id).Should().Equal(reordered);
            var cancelled = await client.Envelopes.CancelAsync(envelope.Id);
            cancelled.Select(item => item.Id).Should().BeEquivalentTo(reordered);
            cancelled.Should().OnlyContain(item => item.Cancelled);
            await client.Envelopes.DeleteAsync(envelope.Id);
            envelope = null;
        }
        finally
        {
            var cleanupErrors = new List<Exception>();
            async Task CleanUp(Func<Task> action)
            {
                try { await RetryRateLimitedCleanupAsync(action); }
                catch (Exception exception) { cleanupErrors.Add(exception); }
            }
            if (envelope is not null)
            {
                await CleanUp(() => client.Envelopes.CancelAsync(envelope.Id));
                await CleanUp(() => client.Envelopes.DeleteAsync(envelope.Id));
            }
            foreach (var document in documents)
                await CleanUp(() => client.Documents.DeleteAsync(document.Id));
            if (createdSigner && signer is not null)
                await CleanUp(() => client.Signers.DeleteAsync(signer.Id));
            if (cleanupErrors.Count > 0)
                throw new AggregateException("Sandbox envelope lifecycle cleanup failed.", cleanupErrors);
        }
    }

    [Fact]
    [Trait("Category", "Live")]
    public async Task EmbedOriginLifecycle_RoundTrips()
    {
        using var client = CreateClient();
        var suffix = Guid.NewGuid().ToString("N");
        EmbedOrigin? origin = null;
        try
        {
            var initial = $"https://sdk-{suffix}.example.com";
            origin = await client.Embed.AddOriginAsync(initial);
            origin.Origin.Should().Be(initial);
            (await client.Embed.ListOriginsAsync()).Should().Contain(item => item.Id == origin.Id);
            var replacement = $"https://sdk-updated-{suffix}.example.com";
            var updated = await client.Embed.UpdateOriginAsync(origin.Id, replacement);
            updated.Id.Should().Be(origin.Id);
            updated.Origin.Should().Be(replacement);
            (await client.Embed.ListOriginsAsync()).Should().Contain(item => item.Id == origin.Id && item.Origin == replacement);
            await client.Embed.DeleteOriginAsync(origin.Id);
            var deletedId = origin.Id;
            origin = null;
            (await client.Embed.ListOriginsAsync()).Should().NotContain(item => item.Id == deletedId);
        }
        finally
        {
            if (origin is not null)
                await RetryRateLimitedCleanupAsync(() => client.Embed.DeleteOriginAsync(origin.Id));
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
