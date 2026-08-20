using System.Text.Json;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

public sealed class AuthenticationResourceTests
{
    private static AuthenticationResource CreateResource(FakeHttpMessageHandler handler)
        => new(FakeHttpMessageHandler.CreateClient(handler));

    [Fact]
    public async Task Login_PostsToLoginEndpoint()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/login",
            FakeHttpMessageHandler.ApiOk(new
            {
                access_token = "token",
                user = new { id = "u1", name = "John", email = "john@example.com", created_at = 1767225600 },
                accounts = new[] { new { id = "acc", name = "Account", roles = new[] { "owner" }, created_at = "2026-01-01" } },
            }));

        var resource = CreateResource(handler);
        var result = await resource.LoginAsync(new LoginRequest { Email = "john@example.com", Password = "secret" });

        result.AccessToken.Should().Be("token");
        result.Accounts.Should().ContainSingle(a => a.Id == "acc");
        handler.Requests.Should().Contain(r => r.RequestUri!.PathAndQuery.Contains("/login"));
    }

    [Fact]
    public async Task ChangePassword_SerializesNewPasswordAsSnakeCase()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Put, "/authentication/change-password",
            FakeHttpMessageHandler.ApiOk(new { email = "john@example.com" }));

        var resource = CreateResource(handler);
        await resource.ChangePasswordAsync(new ChangePasswordRequest
        {
            Email = "john@example.com",
            Password = "old",
            NewPassword = "new",
        });

        var body = JsonDocument.Parse(handler.RequestBodies.Last(b => b.Length > 0));
        body.RootElement.GetProperty("new_password").GetString().Should().Be("new");
    }

    [Fact]
    public async Task PublicAuthentication_DoesNotSendConfiguredCredential()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/login",
            FakeHttpMessageHandler.ApiOk(new { access_token = "token" }));
        handler.AddJsonResponse(HttpMethod.Post, "/authentication/social-login",
            FakeHttpMessageHandler.ApiOk(new { access_token = "token" }));
        handler.AddJsonResponse(HttpMethod.Put, "/authentication/request-password-reset",
            FakeHttpMessageHandler.ApiOk(new { email = "user@example.com" }));
        handler.AddJsonResponse(HttpMethod.Put, "/authentication/reset-password",
            FakeHttpMessageHandler.ApiOk(new { email = "user@example.com" }));

        var resource = new AuthenticationResource(
            FakeHttpMessageHandler.CreateClient(handler),
            request => request.Headers.TryAddWithoutValidation("X-Api-Key", "secret"));

        await resource.LoginAsync(new LoginRequest { Email = "user@example.com", Password = "password" });
        await resource.SocialLoginAsync(new SocialLoginRequest
        {
            Provider = "google",
            Token = "provider-token",
            HasAcceptedTerms = true,
        });
        await resource.RequestPasswordResetAsync(new RequestPasswordResetRequest { Email = "user@example.com" });
        await resource.ResetPasswordAsync(new ResetPasswordRequest
        {
            Email = "user@example.com",
            NewPassword = "new-password",
        });

        handler.Requests.Should().OnlyContain(request => !request.Headers.Contains("X-Api-Key"));
    }
}
