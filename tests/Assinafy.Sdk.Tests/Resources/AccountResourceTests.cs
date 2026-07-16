using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class AccountResourceTests
{
    private static AccountResource CreateResource(FakeHttpMessageHandler handler, string? accountId = "acc")
        => new(FakeHttpMessageHandler.CreateClient(handler), accountId);

    private static object Account(string id, string name) => new
    {
        resource = "account",
        id,
        name,
        primary_color = "2072b9",
        secondary_color = (string?)null,
        notification_sender_type = "User",
        roles = new[] { "owner" },
        is_delete_allowed = true,
        created_at = "2026-01-01T00:00:00Z",
    };

    [Fact]
    public async Task List_GetsAccountsAndDeserializes()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/accounts",
            FakeHttpMessageHandler.ApiOk(new[] { Account("a1", "MT") }));

        var result = await CreateResource(handler).ListAsync();

        result.Should().ContainSingle().Which.Name.Should().Be("MT");
        result[0].Roles.Should().ContainSingle().Which.Should().Be("owner");
        handler.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Get && r.RequestUri!.AbsolutePath.EndsWith("/accounts"));
    }

    [Fact]
    public async Task Get_UsesDefaultAccountId()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/accounts/acc",
            FakeHttpMessageHandler.ApiOk(Account("acc", "MT")));

        var result = await CreateResource(handler).GetAsync();

        result.Id.Should().Be("acc");
        handler.Requests.Should().Contain(r => r.RequestUri!.AbsolutePath.EndsWith("/accounts/acc"));
    }

    [Fact]
    public async Task Get_ThrowsWhenNoAccountId()
    {
        var handler = new FakeHttpMessageHandler();
        var act = () => CreateResource(handler, accountId: null).GetAsync();
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Create_PostsNameAndSenderType()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/accounts",
            FakeHttpMessageHandler.ApiOk(Account("a2", "New Co")));

        var result = await CreateResource(handler).CreateAsync(new CreateAccountRequest
        {
            Name = "New Co",
            NotificationSenderType = AccountNotificationSenderTypes.Account,
        });

        result.Name.Should().Be("New Co");
        var body = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0));
        body.RootElement.GetProperty("name").GetString().Should().Be("New Co");
        body.RootElement.GetProperty("notification_sender_type").GetString().Should().Be("Account");
    }

    [Fact]
    public async Task Create_RequiresName()
    {
        var handler = new FakeHttpMessageHandler();
        var act = () => CreateResource(handler).CreateAsync(new CreateAccountRequest { Name = "" });
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Update_PutsToAccountPathWithOnlySetFields()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/accounts/acc",
            FakeHttpMessageHandler.ApiOk(Account("acc", "Renamed")));

        var result = await CreateResource(handler).UpdateAsync(new UpdateAccountRequest { Name = "Renamed" });

        result.Name.Should().Be("Renamed");
        var request = handler.Requests.Single(r => r.Method == HttpMethod.Put);
        request.RequestUri!.AbsolutePath.Should().EndWith("/accounts/acc");
        var body = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0));
        body.RootElement.GetProperty("name").GetString().Should().Be("Renamed");
        body.RootElement.TryGetProperty("notification_sender_type", out _).Should().BeFalse();
    }

    [Fact]
    public async Task Delete_AddsForceBodyWhenRequested()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Delete, "/accounts/acc",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));

        await CreateResource(handler).DeleteAsync(force: true);

        var request = handler.Requests.Single(r => r.Method == HttpMethod.Delete);
        request.RequestUri!.AbsolutePath.Should().EndWith("/accounts/acc");
        var body = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0));
        body.RootElement.GetProperty("force").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Delete_SendsNoBodyByDefault()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Delete, "/accounts/acc",
            FakeHttpMessageHandler.ApiOk(Array.Empty<object>()));

        await CreateResource(handler).DeleteAsync();

        handler.Requests.Should().Contain(r => r.Method == HttpMethod.Delete && r.Content == null);
    }

    [Fact]
    public async Task GetTheme_ReturnsThemeWithNullableLogo()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/accounts/acc/theme",
            FakeHttpMessageHandler.ApiOk(new
            {
                account_name = "MT",
                primary_color = "2072b9",
                secondary_color = "ffffff",
                logo = (string?)null,
            }));

        var theme = await CreateResource(handler).GetThemeAsync();

        theme.AccountName.Should().Be("MT");
        theme.PrimaryColor.Should().Be("2072b9");
        theme.Logo.Should().BeNull();
    }

    [Fact]
    public async Task DownloadLogo_ReturnsBinary()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Get, "/accounts/acc/logo", "PNGBYTES");

        var bytes = await CreateResource(handler).DownloadLogoAsync();

        bytes.Should().NotBeEmpty();
        handler.Requests.Should().Contain(r => r.RequestUri!.AbsolutePath.EndsWith("/accounts/acc/logo"));
    }

    [Fact]
    public async Task UploadLogo_PostsMultipartFile()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/accounts/acc/logo",
            FakeHttpMessageHandler.ApiOk(new { uploaded = true }));

        using var stream = new MemoryStream(new byte[] { 1, 2, 3 });
        await CreateResource(handler).UploadLogoAsync(stream, "logo.png", "image/png");

        var request = handler.Requests.Single(r => r.Method == HttpMethod.Post);
        request.RequestUri!.AbsolutePath.Should().EndWith("/accounts/acc/logo");
        request.Content.Should().BeOfType<MultipartFormDataContent>();
    }

    [Fact]
    public async Task DeleteLogo_DeletesLogoPath()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Delete, "/accounts/acc/logo",
            FakeHttpMessageHandler.ApiOk(new { deleted = true }));

        await CreateResource(handler).DeleteLogoAsync();

        handler.Requests.Should().Contain(r =>
            r.Method == HttpMethod.Delete && r.RequestUri!.AbsolutePath.EndsWith("/accounts/acc/logo"));
    }
}
