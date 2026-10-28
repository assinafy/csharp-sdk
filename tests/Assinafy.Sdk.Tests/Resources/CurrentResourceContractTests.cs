using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class CurrentResourceContractTests
{
    [Theory]
    [InlineData(null, null, "configured")]
    [InlineData("parameter &account", null, "parameter%20%26account")]
    [InlineData("parameter", "explicit/account", "explicit%2Faccount")]
    public async Task Assignments_List_SelectsAndEscapesWorkspaceWithSorting(
        string? parameterAccount, string? explicitAccount, string expectedAccount)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/assignments", FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));
        var resource = new AssignmentResource(FakeHttpMessageHandler.CreateClient(handler), "configured");

        await resource.ListAsync(new AssignmentListParams
        {
            AccountId = parameterAccount,
            Sort = "-created_at",
            Page = 2,
            PerPage = 25,
        }, explicitAccount);

        handler.Requests.Single().RequestUri!.PathAndQuery.Should().Be(
            $"/v1/assignments?accountId={expectedAccount}&sort=-created_at&page=2&per-page=25");
    }

    [Fact]
    public async Task Assignments_List_AllowsOAuthBoundWorkspaceWithoutAccountId()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/assignments", FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        using var client = new AssinafyClient(new AssinafyClientOptions { Token = "workspace-token" }, http);

        await client.Assignments.ListAsync();

        var sent = handler.Requests.Single();
        sent.RequestUri!.PathAndQuery.Should().Be("/v1/assignments");
        sent.Headers.Authorization!.Parameter.Should().Be("workspace-token");
    }

    [Theory]
    [InlineData(SignerChannels.Email, SignerChannels.Email)]
    [InlineData(SignerChannels.Whatsapp, SignerChannels.Whatsapp)]
    [InlineData(SignerChannels.DigitalCertificate, SignerChannels.Email)]
    [InlineData(SignerChannels.IdDocumentFaceMatchBasic, SignerChannels.Whatsapp)]
    public async Task AssignmentAndTemplateRequests_PreserveVerificationAndNotificationMethods(
        string verification, string notification)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/documents/doc/assignments",
            FakeHttpMessageHandler.ApiOk(new { id = "assignment" }));
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/templates/template/documents",
            FakeHttpMessageHandler.ApiOk(new { id = "document", status = "uploaded" }));
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        var assignments = new AssignmentResource(http, "acc");
        var documents = new DocumentResource(http, "acc");

        await assignments.CreateAsync("doc", new CreateAssignmentRequest
        {
            Signers = [new SignerRef { Id = "signer", VerificationMethod = verification, NotificationMethods = [notification] }],
        });
        await documents.CreateFromTemplateAsync("template",
            [new TemplateSigner { RoleId = "role", Id = "signer", VerificationMethod = verification, NotificationMethods = [notification] }]);

        foreach (var requestBody in handler.RequestBodies)
        {
            using var body = JsonDocument.Parse(requestBody);
            var signer = body.RootElement.GetProperty("signers")[0];
            signer.GetProperty("verification_method").GetString().Should().Be(verification);
            signer.GetProperty("notification_methods").EnumerateArray().Select(value => value.GetString())
                .Should().Equal(notification);
        }
    }

    [Fact]
    public async Task TemplateCreation_AcceptsEmailAndContactFieldsWithIdTakingPrecedence()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/templates/template/documents",
            FakeHttpMessageHandler.ApiOk(new { id = "document", status = "uploaded", assignment = (object?)null }));
        var resource = new DocumentResource(FakeHttpMessageHandler.CreateClient(handler), "acc");

        var result = await resource.CreateFromTemplateAsync("template",
        [
            new TemplateSigner
            {
                RoleId = "role-one", Email = "signer@example.com", Name = "Signer Name",
                WhatsAppPhoneNumber = "+5511999990001", Step = 1,
            },
            new TemplateSigner { RoleId = "role-two", Id = "existing", Email = "ignored@example.com", Name = "Ignored", Step = 2 },
        ]);

        result.Status.Should().Be("uploaded");
        result.Assignment.Should().BeNull();
        handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be("/v1/accounts/acc/templates/template/documents");
        using var body = JsonDocument.Parse(handler.RequestBodies.Single());
        var first = body.RootElement.GetProperty("signers")[0];
        first.GetProperty("email").GetString().Should().Be("signer@example.com");
        first.GetProperty("name").GetString().Should().Be("Signer Name");
        first.GetProperty("whatsapp_phone_number").GetString().Should().Be("+5511999990001");
        first.GetProperty("step").GetInt32().Should().Be(1);
        first.TryGetProperty("id", out _).Should().BeFalse();
        var second = body.RootElement.GetProperty("signers")[1];
        second.GetProperty("id").GetString().Should().Be("existing");
        second.TryGetProperty("email", out _).Should().BeFalse();
        second.TryGetProperty("name", out _).Should().BeFalse();
    }

    [Fact]
    public async Task TemplateCreation_RejectsAbsentSignerIdentityBeforeSending()
    {
        var handler = new FakeHttpMessageHandler();
        var resource = new DocumentResource(FakeHttpMessageHandler.CreateClient(handler), "acc");

        await ((Func<Task>)(() => resource.CreateFromTemplateAsync("template",
            [new TemplateSigner { RoleId = "role", Email = " " }])))
            .Should().ThrowAsync<ValidationException>();

        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task TemplateEstimate_OmitsContactFieldsAndIdentity()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/templates/template/documents/estimate-cost",
            FakeHttpMessageHandler.ApiOk(new { documents = 1, total_credits = 0 }));
        var resource = new DocumentResource(FakeHttpMessageHandler.CreateClient(handler), "acc");

        await resource.EstimateCostFromTemplateAsync("template", [new TemplateSigner
        {
            RoleId = "role", Id = "signer", Email = "signer@example.com", Name = "Signer",
            WhatsAppPhoneNumber = "+5511999990001", Step = 1,
        }]);

        handler.RequestBodies.Single().Should().Be("""{"signers":[{"role_id":"role"}]}""");
    }

    [Fact]
    public async Task DocumentSearch_ForwardsSortingAndPreservesTheOriginalOverload()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/accounts/acc/documents/search",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));
        var resource = new DocumentResource(FakeHttpMessageHandler.CreateClient(handler), "acc");

        await resource.SearchAsync("contract & terms", "certificated", 2, 10, null, "-name", default);
        await resource.SearchAsync("contract", null, null, null, null, default);

        handler.Requests[0].RequestUri!.PathAndQuery.Should().Be(
            "/v1/accounts/acc/documents/search?search=contract%20%26%20terms&status=certificated&page=2&per-page=10&sort=-name");
        handler.Requests[1].RequestUri!.PathAndQuery.Should().Be("/v1/accounts/acc/documents/search?search=contract");
    }

    [Fact]
    public async Task DocumentActivities_ExposePaginationAndPreserveTheOriginalOverload()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/doc%2F1/activities",
            FakeHttpMessageHandler.ApiOk(new[] { new { id = 1, @event = "document_ready" } }),
            headers: new Dictionary<string, string>
            {
                ["X-Pagination-Current-Page"] = "2",
                ["X-Pagination-Per-Page"] = "10",
                ["X-Pagination-Total-Count"] = "15",
                ["X-Pagination-Page-Count"] = "2",
            });
        var resource = new DocumentResource(FakeHttpMessageHandler.CreateClient(handler), "acc");

        var page = await resource.ActivitiesAsync("doc/1", new DocumentActivityListParams
        {
            Sort = "id",
            Page = 2,
            PerPage = 10,
        }, default);
        var legacy = await resource.ActivitiesAsync("doc/1", default);

        handler.Requests[0].RequestUri!.PathAndQuery.Should().Be("/v1/documents/doc%2F1/activities?sort=id&page=2&per-page=10");
        handler.Requests[1].RequestUri!.PathAndQuery.Should().Be("/v1/documents/doc%2F1/activities");
        page.Meta!.CurrentPage.Should().Be(2);
        page.Meta.PerPage.Should().Be(10);
        page.Meta.Total.Should().Be(15);
        page.Meta.LastPage.Should().Be(2);
        page.Data.Should().ContainSingle();
        legacy.Should().ContainSingle();
    }

    [Theory]
    [InlineData("ContractRenewalNotice")]
    [InlineData("ContractExpiring")]
    [InlineData("ContractExpired")]
    [InlineData("ContractRenewed")]
    public async Task NotificationPreferences_AcceptEachContractPreferenceAlone(string preference)
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/users/self/notification-preferences",
            FakeHttpMessageHandler.ApiOk(new Dictionary<string, bool> { [preference] = false }));
        var resource = new UserResource(FakeHttpMessageHandler.CreateClient(handler));
        var request = new UpdateNotificationPreferencesRequest
        {
            ContractRenewalNotice = preference == "ContractRenewalNotice" ? false : null,
            ContractExpiring = preference == "ContractExpiring" ? false : null,
            ContractExpired = preference == "ContractExpired" ? false : null,
            ContractRenewed = preference == "ContractRenewed" ? false : null,
        };

        await resource.UpdateNotificationPreferencesAsync(request);

        handler.RequestBodies.Single().Should().Be($"{{\"{preference}\":false}}");
    }

    [Fact]
    public async Task OAuthWebhookUpdateAndRotation_ReturnGeneratedSecrets()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/webhooks/endpoints/endpoint",
            FakeHttpMessageHandler.ApiOk(new { id = "endpoint", signing_enabled = true, secret = "whsec_ZXhhbXBsZQ==" }));
        handler.AddJsonResponse(HttpMethod.Post, "/webhooks/endpoints/endpoint/secret/rotate",
            FakeHttpMessageHandler.ApiOk(new { secret = "whsec_cm90YXRlZA==" }));
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        using var client = new AssinafyClient(new AssinafyClientOptions { Token = "oauth-token", AccountId = "acc" }, http);

        var endpoint = await client.Webhooks.UpdateEndpointAsync("endpoint", new UpdateWebhookEndpointRequest { SigningEnabled = true });
        var rotated = await client.Webhooks.RotateEndpointSecretAsync("endpoint");

        endpoint.Secret.Should().Be("whsec_ZXhhbXBsZQ==");
        rotated.Secret.Should().Be("whsec_cm90YXRlZA==");
        handler.RequestBodies[0].Should().Be("""{"signing_enabled":true}""");
        handler.Requests.Select(request => (request.Method, request.RequestUri!.AbsolutePath)).Should().Equal(
            (HttpMethod.Put, "/v1/accounts/acc/webhooks/endpoints/endpoint"),
            (HttpMethod.Post, "/v1/accounts/acc/webhooks/endpoints/endpoint/secret/rotate"));
        handler.Requests.Should().OnlyContain(request => request.Headers.Authorization!.Parameter == "oauth-token");
    }
}
