using System.Text;
using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class DocumentResourceTests
{
    private static DocumentResource CreateResource(FakeHttpMessageHandler handler, string? accountId = "acc")
        => new(FakeHttpMessageHandler.CreateClient(handler), accountId);

    [Fact]
    public async Task Upload_ThrowsForNonPdfFile()
    {
        var handler = new FakeHttpMessageHandler();
        var resource = CreateResource(handler);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));

        var act = () => resource.UploadAsync(stream, "document.docx");

        await act.Should().ThrowAsync<ValidationException>().WithMessage("*PDF*");
    }

    [Fact]
    public async Task Upload_PostsToAccountsDocumentsWithoutDroppingV1()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/v1/accounts/acc/documents",
            FakeHttpMessageHandler.ApiOk(new
            {
                id = "doc-1",
                name = "contract.pdf",
                status = "uploaded",
                account_id = "acc",
                created_at = "2026-01-01",
                updated_at = "2026-01-01",
                is_closed = false,
                pages = Array.Empty<object>(),
            }));

        var resource = CreateResource(handler);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("pdf-data"));
        var result = await resource.UploadAsync(stream, "contract.pdf");

        result.Id.Should().Be("doc-1");
        stream.CanRead.Should().BeTrue("the SDK must not dispose caller-owned streams");
        handler.Requests.Should().Contain(r =>
            r.RequestUri!.PathAndQuery.Contains("/v1/accounts/acc/documents") &&
            r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task WaitUntilReady_RejectsNonPositiveTimingValues()
    {
        var resource = CreateResource(new FakeHttpMessageHandler());

        await ((Func<Task>)(() => resource.WaitUntilReadyAsync("doc-1", TimeSpan.Zero)))
            .Should().ThrowAsync<ValidationException>();
        await ((Func<Task>)(() => resource.WaitUntilReadyAsync(
                "doc-1", TimeSpan.FromSeconds(1), TimeSpan.Zero)))
            .Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task ListStatuses_CallsDocumentStatuses()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/statuses",
            FakeHttpMessageHandler.ApiOk(new[] { new { code = "uploaded", deletable = false } }));

        var resource = CreateResource(handler);
        var result = await resource.ListStatusesAsync();

        result.Should().ContainSingle(s => s.Code == "uploaded");
    }

    [Fact]
    public async Task List_ReturnsPaginationMetaFromHeaders()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(
            HttpMethod.Get,
            "/accounts/acc/documents",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()),
            headers: new Dictionary<string, string>
            {
                ["x-pagination-current-page"] = "2",
                ["x-pagination-per-page"] = "20",
                ["x-pagination-total-count"] = "45",
                ["x-pagination-page-count"] = "3",
            });

        var resource = CreateResource(handler);
        var result = await resource.ListAsync(new Dictionary<string, string?> { ["page"] = "2" });

        result.Meta!.CurrentPage.Should().Be(2);
        result.Meta.PerPage.Should().Be(20);
        result.Meta.Total.Should().Be(45);
        result.Meta.LastPage.Should().Be(3);
    }

    [Fact]
    public async Task Get_CallsDocumentsEndpoint()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/doc-1",
            FakeHttpMessageHandler.ApiOk(new
            {
                id = "doc-1",
                name = "contract.pdf",
                status = "pending_signature",
                created_at = 1767225600,
                updated_at = 1767225601,
                is_closed = false,
                pages = Array.Empty<object>(),
            }));

        var resource = CreateResource(handler);
        var result = await resource.GetAsync("doc-1");

        result.Id.Should().Be("doc-1");
        result.CreatedAt.Should().Be("1767225600");
    }

    [Fact]
    public async Task CreateFromTemplate_PostsToCorrectEndpoint()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/templates/tmpl-1/documents",
            FakeHttpMessageHandler.ApiOk(new
            {
                id = "doc-1",
                name = "From Template",
                status = "uploaded",
                created_at = "2026-01-01",
                updated_at = "2026-01-01",
                is_closed = false,
                pages = Array.Empty<object>(),
            }));

        var resource = CreateResource(handler);
        var result = await resource.CreateFromTemplateAsync(
            "tmpl-1",
            [new TemplateSigner { RoleId = "role-1", Id = "signer-1" }]);

        result.Id.Should().Be("doc-1");
        handler.Requests.Should().Contain(r =>
            r.RequestUri!.PathAndQuery.Contains("/accounts/acc/templates/tmpl-1/documents"));
    }

    [Fact]
    public async Task CreateFromTemplate_RejectsMissingEditorValueBeforeTransport()
    {
        var handler = new FakeHttpMessageHandler();
        var resource = CreateResource(handler);

        var act = () => resource.CreateFromTemplateAsync(
            "tmpl-1",
            [new TemplateSigner { RoleId = "role-1", Id = "signer-1" }],
            new CreateDocumentFromTemplateOptions
            {
                EditorFields = [new TemplateEditorField { FieldId = "field-1", Value = null! }],
            });

        await act.Should().ThrowAsync<ValidationException>()
            .WithMessage("*editor field value*");
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task EstimateFromTemplate_SendsOnlyEstimateSignerFields()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/templates/tmpl-1/documents/estimate-cost",
            FakeHttpMessageHandler.ApiOk(new
            {
                documents = 1,
                total_credits = 0.45m,
                has_sufficient_resources = true,
            }));

        var resource = CreateResource(handler);
        await resource.EstimateCostFromTemplateAsync(
            "tmpl-1",
            [new TemplateSigner { RoleId = "role-1", VerificationMethod = SignerChannels.Whatsapp }]);

        var signer = JsonDocument.Parse(handler.RequestBodies.Last()).RootElement
            .GetProperty("signers")[0];
        signer.GetProperty("role_id").GetString().Should().Be("role-1");
        signer.GetProperty("verification_method").GetString().Should().Be("Whatsapp");
        signer.TryGetProperty("id", out _).Should().BeFalse();
        signer.TryGetProperty("step", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Verify_CallsVerifyEndpoint()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/documents/abc123/verify",
            FakeHttpMessageHandler.ApiOk(new { hash = "abc123", is_valid = true }));

        var resource = CreateResource(handler);
        var result = await resource.VerifyAsync("abc123");

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Delete_CallsDeleteEndpoint()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Delete, "/documents/doc-1",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));

        var resource = CreateResource(handler);
        await resource.DeleteAsync("doc-1");

        handler.Requests.Should().Contain(r =>
            r.RequestUri!.PathAndQuery.Contains("/documents/doc-1") &&
            r.Method == HttpMethod.Delete);
    }
}
