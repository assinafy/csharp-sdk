using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;
using Assinafy.Sdk.Resources;
using Assinafy.Sdk.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace Assinafy.Sdk.Tests.Resources;

/// <summary>
/// Contract coverage for the OAuth 2.1 endpoints: the RFC-mandated form encoding on the way out,
/// the flat (un-enveloped) bodies on the way back, PKCE generation, and the error codes that drive
/// reconnect logic.
/// </summary>
public sealed class OAuthResourceTests
{
    private static HttpClient Client(FakeHttpMessageHandler handler) => FakeHttpMessageHandler.CreateClient(handler);

    private static OAuthCodeExchangeRequest ValidExchange() => new()
    {
        Code = "the-code",
        RedirectUri = "https://myapp.example.com/oauth/callback",
        CodeVerifier = new string('a', 43),
        ClientId = "client-1",
    };

    private static Dictionary<string, string> Form(string body) =>
        body.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                parts => Uri.UnescapeDataString(parts[0]),
                parts => parts.Length > 1 ? HttpUtility.UrlDecode(parts[1]) : string.Empty,
                StringComparer.Ordinal);

    // ---- PKCE and state --------------------------------------------------------------

    [Fact]
    public void CreatePkcePair_ProducesAnRfc7636VerifierAndItsS256Challenge()
    {
        var pair = OAuthResource.CreatePkcePair();

        pair.CodeVerifier.Should().HaveLength(43);
        pair.CodeVerifier.Should().MatchRegex("^[A-Za-z0-9._~-]+$");
        pair.CodeChallengeMethod.Should().Be("S256");

        var expected = Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(pair.CodeVerifier)))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        pair.CodeChallenge.Should().Be(expected);
    }

    [Fact]
    public void CreatePkcePair_IsUniquePerAttempt()
    {
        OAuthResource.CreatePkcePair().CodeVerifier
            .Should().NotBe(OAuthResource.CreatePkcePair().CodeVerifier);
    }

    [Fact]
    public void CreateState_IsUrlSafeAndUnique()
    {
        var state = OAuthResource.CreateState();

        state.Should().MatchRegex("^[A-Za-z0-9_-]+$");
        state.Should().NotBe(OAuthResource.CreateState());
    }

    // ---- Authorization URL -----------------------------------------------------------

    [Fact]
    public void BuildAuthorizationUrl_CarriesEveryRequiredParameter()
    {
        var url = OAuthResource.BuildAuthorizationUrl(new OAuthAuthorizationRequest
        {
            ClientId = "client-1",
            RedirectUri = "https://myapp.example.com/oauth/callback",
            Scopes = [OAuthScopes.DocumentsRead, OAuthScopes.WebhooksWrite, OAuthScopes.OfflineAccess],
            State = "state-1",
            CodeChallenge = "challenge-1",
        });

        url.GetLeftPart(UriPartial.Path).Should().Be(OAuthResource.DefaultAuthorizationEndpoint);

        var query = HttpUtility.ParseQueryString(url.Query);
        query["response_type"].Should().Be("code");
        query["client_id"].Should().Be("client-1");
        query["redirect_uri"].Should().Be("https://myapp.example.com/oauth/callback");
        query["scope"].Should().Be("documents:read webhooks:write offline_access");
        query["state"].Should().Be("state-1");
        query["code_challenge"].Should().Be("challenge-1");
        query["code_challenge_method"].Should().Be("S256");
        query["resource"].Should().Be(OAuthResource.DefaultResource);
        query["nonce"].Should().BeNull();
    }

    [Fact]
    public void BuildAuthorizationUrl_IncludesNonceAndHonoursEndpointOverride()
    {
        var url = OAuthResource.BuildAuthorizationUrl(new OAuthAuthorizationRequest
        {
            ClientId = "client-1",
            RedirectUri = "https://myapp.example.com/cb",
            Scopes = [OAuthScopes.OpenId],
            State = "s",
            CodeChallenge = "c",
            Nonce = "nonce-1",
            AuthorizationEndpoint = "https://auth-sandbox.assinafy.com.br/oauth/authorize",
        });

        url.Host.Should().Be("auth-sandbox.assinafy.com.br");
        HttpUtility.ParseQueryString(url.Query)["nonce"].Should().Be("nonce-1");
    }

    [Fact]
    public void BuildAuthorizationUrl_RejectsMissingScopesAndNonHttpsEndpoint()
    {
        var baseRequest = () => new OAuthAuthorizationRequest
        {
            ClientId = "c",
            RedirectUri = "https://myapp.example.com/cb",
            Scopes = [OAuthScopes.DocumentsRead],
            State = "s",
            CodeChallenge = "ch",
        };

        var noScopes = baseRequest();
        noScopes.Scopes = [];
        ((Action)(() => OAuthResource.BuildAuthorizationUrl(noScopes)))
            .Should().Throw<ValidationException>();

        var insecure = baseRequest();
        insecure.AuthorizationEndpoint = "http://auth.assinafy.com.br/oauth/authorize";
        ((Action)(() => OAuthResource.BuildAuthorizationUrl(insecure)))
            .Should().Throw<ValidationException>();
    }

    // ---- Token exchange --------------------------------------------------------------

    [Fact]
    public async Task ExchangeCodeAsync_PostsFormEncodedGrantAndReadsTheFlatResponse()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/oauth/token", new
        {
            access_token = "at-1",
            token_type = "Bearer",
            expires_in = 3600,
            refresh_token = "rt-1",
            scope = "documents:read documents:write",
            id_token = "idt-1",
        });

        var resource = new OAuthResource(Client(handler));
        var request = ValidExchange();
        request.ClientSecret = "secret-1";
        var result = await resource.ExchangeCodeAsync(request);

        result.AccessToken.Should().Be("at-1");
        result.TokenType.Should().Be("Bearer");
        result.ExpiresIn.Should().Be(3600);
        result.RefreshToken.Should().Be("rt-1");
        result.IdToken.Should().Be("idt-1");
        result.GrantedScopes.Should().Equal("documents:read", "documents:write");
        result.HasScope(OAuthScopes.DocumentsWrite).Should().BeTrue();
        result.HasScope(OAuthScopes.TemplatesWrite).Should().BeFalse();

        var sent = handler.Requests.Single();
        sent.RequestUri!.AbsolutePath.Should().Be("/v1/oauth/token");
        sent.Content!.Headers.ContentType!.MediaType.Should().Be("application/x-www-form-urlencoded");

        var form = Form(handler.RequestBodies.Single());
        form["grant_type"].Should().Be("authorization_code");
        form["code"].Should().Be("the-code");
        form["redirect_uri"].Should().Be("https://myapp.example.com/oauth/callback");
        form["code_verifier"].Should().Be(new string('a', 43));
        form["client_id"].Should().Be("client-1");
        form["client_secret"].Should().Be("secret-1");
        form["resource"].Should().Be(OAuthResource.DefaultResource);
    }

    [Fact]
    public async Task ExchangeCodeAsync_OmitsClientSecretForPublicApplications()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/oauth/token",
            new { access_token = "at", token_type = "Bearer", expires_in = 3600 });

        var resource = new OAuthResource(Client(handler));
        await resource.ExchangeCodeAsync(ValidExchange());

        Form(handler.RequestBodies.Single()).Should().NotContainKey("client_secret");
    }

    [Fact]
    public async Task ExchangeCodeAsync_NeverSendsTheConfiguredClientCredential()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/oauth/token",
            new { access_token = "at", token_type = "Bearer", expires_in = 3600 });

        var resource = new OAuthResource(
            Client(handler),
            request => request.Headers.Add("X-Api-Key", "configured-key"));
        await resource.ExchangeCodeAsync(ValidExchange());

        handler.Requests.Single().Headers.Should().NotContain(h => h.Key == "X-Api-Key");
    }

    [Fact]
    public async Task ExchangeCodeAsync_RejectsAnOutOfGrammarVerifierBeforeSpendingTheCode()
    {
        var handler = new FakeHttpMessageHandler();
        var resource = new OAuthResource(Client(handler));

        var tooShort = ValidExchange();
        tooShort.CodeVerifier = new string('a', 42);
        await ((Func<Task>)(() => resource.ExchangeCodeAsync(tooShort)))
            .Should().ThrowAsync<ValidationException>();

        var illegalCharacter = ValidExchange();
        illegalCharacter.CodeVerifier = new string('a', 42) + "/";
        await ((Func<Task>)(() => resource.ExchangeCodeAsync(illegalCharacter)))
            .Should().ThrowAsync<ValidationException>();

        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ExchangeCodeAsync_MapsAFlatOAuthErrorToOAuthException()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(
            HttpMethod.Post,
            "/oauth/token",
            new { error = "invalid_grant", error_description = "The authorization code has expired." },
            HttpStatusCode.BadRequest);

        var resource = new OAuthResource(Client(handler));
        var thrown = await ((Func<Task>)(() => resource.ExchangeCodeAsync(ValidExchange())))
            .Should().ThrowAsync<OAuthException>();

        thrown.Which.Error.Should().Be("invalid_grant");
        thrown.Which.ErrorDescription.Should().Be("The authorization code has expired.");
        thrown.Which.StatusCode.Should().Be(400);
        thrown.Which.Should().BeAssignableTo<ApiException>();
    }

    [Fact]
    public async Task ExchangeCodeAsync_TreatsAResponseWithoutAnAccessTokenAsAContractError()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/oauth/token", new { token_type = "Bearer" });

        var resource = new OAuthResource(Client(handler));
        await ((Func<Task>)(() => resource.ExchangeCodeAsync(ValidExchange())))
            .Should().ThrowAsync<SerializationException>();
    }

    // ---- Refresh and revoke ----------------------------------------------------------

    [Fact]
    public async Task RefreshTokenAsync_PostsTheRefreshGrantAndReturnsTheRotatedToken()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Post, "/oauth/token", new
        {
            access_token = "at-2",
            token_type = "Bearer",
            expires_in = 3600,
            refresh_token = "rt-2",
        });

        var resource = new OAuthResource(Client(handler));
        var result = await resource.RefreshTokenAsync(new OAuthRefreshRequest
        {
            RefreshToken = "rt-1",
            ClientId = "client-1",
            ClientSecret = "secret-1",
        });

        result.RefreshToken.Should().Be("rt-2");

        var form = Form(handler.RequestBodies.Single());
        form["grant_type"].Should().Be("refresh_token");
        form["refresh_token"].Should().Be("rt-1");
        form["client_id"].Should().Be("client-1");
        form.Should().NotContainKey("resource");
    }

    [Fact]
    public async Task RevokeAsync_PostsTheTokenAndAcceptsAnEmptyBody()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddRawResponse(HttpMethod.Post, "/oauth/revoke", string.Empty);

        var resource = new OAuthResource(Client(handler));
        await resource.RevokeAsync(new OAuthRevokeRequest
        {
            Token = "rt-1",
            ClientId = "client-1",
            TokenTypeHint = "refresh_token",
        });

        handler.Requests.Single().RequestUri!.AbsolutePath.Should().Be("/v1/oauth/revoke");
        var form = Form(handler.RequestBodies.Single());
        form["token"].Should().Be("rt-1");
        form["client_id"].Should().Be("client-1");
        form["token_type_hint"].Should().Be("refresh_token");
    }

    [Fact]
    public async Task RevokeAsync_SurfacesInvalidClient()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(
            HttpMethod.Post,
            "/oauth/revoke",
            new { error = "invalid_client", error_description = "Client authentication failed." },
            HttpStatusCode.Unauthorized);

        var resource = new OAuthResource(Client(handler));
        var thrown = await ((Func<Task>)(() => resource.RevokeAsync(new OAuthRevokeRequest
        {
            Token = "rt-1",
            ClientId = "client-1",
        }))).Should().ThrowAsync<OAuthException>();

        thrown.Which.Error.Should().Be("invalid_client");
    }

    // ---- Userinfo and discovery ------------------------------------------------------

    [Fact]
    public async Task GetUserInfoAsync_ReadsFlatClaimsWithTheConfiguredBearerToken()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/oauth/userinfo", new
        {
            sub = "user-1",
            name = "Maria Silva",
            email = "maria@example.com",
            email_verified = true,
        });

        var resource = new OAuthResource(
            Client(handler),
            request => request.Headers.Authorization = new("Bearer", "at-1"));
        var result = await resource.GetUserInfoAsync();

        result.Sub.Should().Be("user-1");
        result.Name.Should().Be("Maria Silva");
        result.Email.Should().Be("maria@example.com");
        result.EmailVerified.Should().BeTrue();
        handler.Requests.Single().Headers.Authorization!.Parameter.Should().Be("at-1");
    }

    [Fact]
    public async Task GetProtectedResourceMetadataAsync_ReadsTheRootWellKnownDocument()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(HttpMethod.Get, "/.well-known/oauth-protected-resource", new
        {
            resource = "https://api.assinafy.com.br",
            authorization_servers = new[] { "https://auth.assinafy.com.br" },
            scopes_supported = new[] { "documents:read", "documents:write" },
            bearer_methods_supported = new[] { "header" },
        });

        var resource = new OAuthResource(Client(handler));
        var result = await resource.GetProtectedResourceMetadataAsync();

        result.Resource.Should().Be("https://api.assinafy.com.br");
        result.AuthorizationServers.Should().ContainSingle().Which.Should().Be("https://auth.assinafy.com.br");
        result.ScopesSupported.Should().Contain("documents:write");
        result.BearerMethodsSupported.Should().Equal("header");

        // RFC 8615 places the document at the host root, outside the /v1 base path.
        handler.Requests.Single().RequestUri!.AbsolutePath
            .Should().Be("/.well-known/oauth-protected-resource");
    }

    // ---- insufficient_scope on an ordinary endpoint ----------------------------------

    [Fact]
    public async Task MissingScopeOnAnEnvelopeEndpoint_SurfacesTheScopeToRequest()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(
            HttpMethod.Get,
            "/accounts/acc/documents",
            new { status = 403, message = "Forbidden", data = (object?)null },
            HttpStatusCode.Forbidden,
            new Dictionary<string, string>
            {
                ["WWW-Authenticate"] =
                    "Bearer error=\"insufficient_scope\", scope=\"documents:write\", " +
                    "resource_metadata=\"https://api.assinafy.com.br/.well-known/oauth-protected-resource\"",
            });

        var documents = new DocumentResource(Client(handler), "acc");
        var thrown = await ((Func<Task>)(() => documents.ListAsync()))
            .Should().ThrowAsync<OAuthException>();

        thrown.Which.Error.Should().Be("insufficient_scope");
        thrown.Which.Scope.Should().Be("documents:write");
        thrown.Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task ForbiddenWithoutAChallenge_StaysAPlainApiException()
    {
        var handler = new FakeHttpMessageHandler();
        handler.AddJsonResponse(
            HttpMethod.Get,
            "/accounts/acc/documents",
            new { status = 403, message = "Forbidden", data = (object?)null },
            HttpStatusCode.Forbidden);

        var documents = new DocumentResource(Client(handler), "acc");
        var thrown = await ((Func<Task>)(() => documents.ListAsync()))
            .Should().ThrowAsync<ApiException>();

        thrown.Which.Should().NotBeOfType<OAuthException>();
        thrown.Which.StatusCode.Should().Be(403);
    }
}
