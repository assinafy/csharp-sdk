using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests;

public sealed class ReliabilityRegressionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void SuppliedHttpClient_IgnoresInvalidOptionTimeout(int timeoutMilliseconds)
    {
        var handler = new FakeHttpMessageHandler();
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        var originalTimeout = TimeSpan.FromSeconds(17);
        http.Timeout = originalTimeout;

        using var client = new AssinafyClient(new AssinafyClientOptions
        {
            ApiKey = "key",
            AccountId = "account",
            Timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds),
        }, http);

        http.Timeout.Should().Be(originalTimeout);
        handler.Requests.Should().BeEmpty();
    }

    [Theory]
    [InlineData("key\r\nInjected: value", null)]
    [InlineData(null, "invalid token")]
    public void InvalidCredentials_AreRejectedBeforeRequest(string? apiKey, string? token)
    {
        var handler = new FakeHttpMessageHandler();
        using var http = FakeHttpMessageHandler.CreateClient(handler);

        var act = () =>
        {
            using var client = new AssinafyClient(new AssinafyClientOptions
            {
                ApiKey = apiKey,
                Token = token,
                AccountId = "account",
            }, http);
        };

        act.Should().Throw<ValidationException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task OutboundSerializationFailure_IsWrappedBeforeRequest()
    {
        var handler = new FakeHttpMessageHandler();
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        var resource = new FieldResource(http, "account");
        var cyclicValue = new List<object>();
        cyclicValue.Add(cyclicValue);

        var act = () => resource.ValidateAsync(
            "field",
            new ValidateFieldValueRequest { Value = cyclicValue });

        var exception = (await act.Should().ThrowAsync<SerializationException>()).Which;
        exception.InnerException.Should().BeOfType<JsonException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task WaitUntilReady_RetriesNotFoundUntilDeadlineAndPreservesFailure()
    {
        var handler = new FakeHttpMessageHandler();
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        var resource = new DocumentResource(http, "account");

        var warmup = () => resource.GetAsync("warmup");
        (await warmup.Should().ThrowAsync<ApiException>()).Which.StatusCode.Should().Be(404);
        handler.Requests.Clear();
        handler.RequestBodies.Clear();

        var act = () => resource.WaitUntilReadyAsync(
            "document",
            TimeSpan.FromSeconds(1),
            TimeSpan.FromMilliseconds(1));

        var exception = (await act.Should().ThrowAsync<ValidationException>()).Which;
        exception.InnerException.Should().BeOfType<ApiException>()
            .Which.StatusCode.Should().Be(404);
        handler.Requests.Count.Should().BeGreaterThan(3);
    }

    [Fact]
    public async Task FindByEmail_PagesWithoutPaginationMetadata()
    {
        var handler = new FakeHttpMessageHandler();
        var firstPage = new[]
        {
            new { id = "other", full_name = "Other", email = "other@example.invalid" },
        };
        handler.AddJsonResponse(
            HttpMethod.Get,
            "&page=1",
            FakeHttpMessageHandler.ApiOk(firstPage));
        handler.AddJsonResponse(
            HttpMethod.Get,
            "&page=2",
            FakeHttpMessageHandler.ApiOk(new[]
            {
                new { id = "target", full_name = "Target", email = "target@example.invalid" },
            }));
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        var resource = new SignerResource(http, "account");

        var result = await resource.FindByEmailAsync("target@example.invalid");

        result.Should().NotBeNull();
        result!.Id.Should().Be("target");
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task FindByEmail_StopsWhenServerRepeatsAPageWithoutMetadata()
    {
        var handler = new FakeHttpMessageHandler();
        var repeatedPage = new[]
        {
            new { id = "other", full_name = "Other", email = "other@example.invalid" },
        };
        handler.AddJsonResponse(HttpMethod.Get, "&page=1", FakeHttpMessageHandler.ApiOk(repeatedPage));
        handler.AddJsonResponse(HttpMethod.Get, "&page=2", FakeHttpMessageHandler.ApiOk(repeatedPage));
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        var resource = new SignerResource(http, "account");

        var result = await resource.FindByEmailAsync("target@example.invalid");

        result.Should().BeNull();
        handler.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task PdfUploads_RejectUnreadableStreamsBeforeRequest()
    {
        var handler = new FakeHttpMessageHandler();
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        var documents = new DocumentResource(http, "account");
        var templates = new TemplateResource(http, "account");
        var documentStream = new MemoryStream([1]);
        var templateStream = new MemoryStream([1]);
        documentStream.Dispose();
        templateStream.Dispose();

        await ((Func<Task>)(() => documents.UploadAsync(documentStream, "document.pdf")))
            .Should().ThrowAsync<ValidationException>();
        await ((Func<Task>)(() => templates.CreateAsync(templateStream, "template.pdf")))
            .Should().ThrowAsync<ValidationException>();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PdfUploads_RejectPositionPastLengthBeforeRequest()
    {
        var handler = new FakeHttpMessageHandler();
        using var http = FakeHttpMessageHandler.CreateClient(handler);
        var documents = new DocumentResource(http, "account");
        var templates = new TemplateResource(http, "account");
        using var documentStream = new MemoryStream([1]);
        using var templateStream = new MemoryStream([1]);
        documentStream.Position = documentStream.Length + 1;
        templateStream.Position = templateStream.Length + 1;

        await ((Func<Task>)(() => documents.UploadAsync(documentStream, "document.pdf")))
            .Should().ThrowAsync<ValidationException>();
        await ((Func<Task>)(() => templates.CreateAsync(templateStream, "template.pdf")))
            .Should().ThrowAsync<ValidationException>();
        handler.Requests.Should().BeEmpty();
    }
}
