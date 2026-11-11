using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Allowed embed origins and short-lived, single-use embedded signing sessions.</summary>
public sealed class EmbedResource : BaseResource
{
    internal EmbedResource(HttpClient http, string? defaultAccountId = null, Action<HttpRequestMessage>? authenticate = null)
        : base(http, defaultAccountId, authenticate) { }

    /// <summary><c>GET /accounts/{accountId}/embed-origins</c> — list allowed origins, oldest first. Requires the Embed feature; unavailable to OAuth applications.</summary>
    /// <param name="accountId">Workspace ID, or the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The workspace's registered origins.</returns>
    public Task<IReadOnlyList<EmbedOrigin>> ListOriginsAsync(string? accountId = null, CancellationToken cancellationToken = default) =>
        CallListBodyAsync<EmbedOrigin>($"accounts/{AccountId(accountId)}/embed-origins", HttpMethod.Get, cancellationToken: cancellationToken);

    /// <summary><c>POST /accounts/{accountId}/embed-origins</c> — allow an exact HTTPS origin, or HTTP localhost/127.0.0.1. Maximum ten origins; duplicates return 400. Requires Embed; unavailable to OAuth applications.</summary>
    /// <param name="origin">Scheme, host, and optional port, without path, query, fragment, credentials, or wildcard.</param>
    /// <param name="accountId">Workspace ID, or the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The registered origin, normalized by the API.</returns>
    public Task<EmbedOrigin> AddOriginAsync(string origin, string? accountId = null, CancellationToken cancellationToken = default)
    {
        ValidateOrigin(origin);
        return CallAsync<EmbedOrigin>($"accounts/{AccountId(accountId)}/embed-origins", HttpMethod.Post,
            new { origin }, cancellationToken: cancellationToken);
    }

    /// <summary><c>PUT /accounts/{accountId}/embed-origins/{originId}</c> — replace an origin while retaining its ID. The same origin rules as creation apply; unavailable to OAuth applications.</summary>
    /// <param name="originId">Registration to update.</param>
    /// <param name="origin">Exact replacement origin.</param>
    /// <param name="accountId">Workspace ID, or the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated origin.</returns>
    public Task<EmbedOrigin> UpdateOriginAsync(string originId, string origin, string? accountId = null, CancellationToken cancellationToken = default)
    {
        ValidateOrigin(origin);
        return CallAsync<EmbedOrigin>($"accounts/{AccountId(accountId)}/embed-origins/{PathSegment(originId, "Origin ID")}",
            HttpMethod.Put, new { origin }, cancellationToken: cancellationToken);
    }

    /// <summary><c>DELETE /accounts/{accountId}/embed-origins/{originId}</c> — remove an allowed origin. Removing the last prevents new sessions; unavailable to OAuth applications.</summary>
    /// <param name="originId">Registration to remove.</param>
    /// <param name="accountId">Workspace ID, or the configured default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes after removal.</returns>
    public Task DeleteOriginAsync(string originId, string? accountId = null, CancellationToken cancellationToken = default) =>
        CallVoidAsync($"accounts/{AccountId(accountId)}/embed-origins/{PathSegment(originId, "Origin ID")}", HttpMethod.Delete,
            cancellationToken: cancellationToken);

    /// <summary><c>POST /documents/{documentId}/assignments/{assignmentId}/signers/{signerId}/embed-sessions</c> — mint a five-minute, single-use iframe URL for an embedded signer. Requires Embed and an allowed origin; limited to 30 sessions per signer per hour.</summary>
    /// <remarks>Mint a fresh URL whenever the page opens. The API returns 409 for unavailable documents, pending steps, completed signers, or missing origins. No SDK retries.</remarks>
    /// <param name="documentId">Document to sign.</param>
    /// <param name="assignmentId">Assignment containing the signer.</param>
    /// <param name="signerId">Signer created with embedded set to true.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The secret iframe URL and its expiry.</returns>
    public Task<EmbedSession> CreateSessionAsync(string documentId, string assignmentId, string signerId, CancellationToken cancellationToken = default) =>
        CallAsync<EmbedSession>($"documents/{PathSegment(documentId, "Document ID")}/assignments/{PathSegment(assignmentId, "Assignment ID")}/signers/{PathSegment(signerId, "Signer ID")}/embed-sessions",
            HttpMethod.Post, cancellationToken: cancellationToken);

    /// <summary><c>POST /embed/sessions/{token}/exchange</c> — consume a session token and return its signer credential. Intended for Assinafy's embed page, not integrating backends. Sends no workspace credential.</summary>
    /// <remarks>The token is consumed even on 409. Unknown, expired, or reused tokens return 410. Never retry the exchange.</remarks>
    /// <param name="token">The single-use token from the iframe URL.</param>
    /// <param name="parentOrigin">Optional embedding-page origin recorded by the API.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The authorized signer session and allowed origins.</returns>
    public Task<EmbedSessionExchange> ExchangeSessionAsync(string token, string? parentOrigin = null, CancellationToken cancellationToken = default)
    {
        if (parentOrigin is not null) ValidateOrigin(parentOrigin);
        return CallAsync<EmbedSessionExchange>($"embed/sessions/{PathSegment(token, "Embed token")}/exchange", HttpMethod.Post,
            parentOrigin is null ? null : new { parent_origin = parentOrigin }, cancellationToken: cancellationToken, authenticate: false);
    }

    private static void ValidateOrigin(string origin)
    {
        if (string.IsNullOrWhiteSpace(origin) || origin.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)) ||
            origin.Contains('*') || origin.Contains('\\') || !origin.Contains("://", StringComparison.Ordinal) ||
            !Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
             !(uri.Scheme == Uri.UriSchemeHttp && uri.Host is "localhost" or "127.0.0.1")) ||
            uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0)
            throw new ValidationException("An embed origin must be an exact HTTPS origin, or HTTP localhost/127.0.0.1, without path, query, fragment, credentials, or wildcard.");
        var authorityEnd = origin.IndexOfAny(['/', '?', '#'], origin.IndexOf("://", StringComparison.Ordinal) + 3);
        if (authorityEnd >= 0 && origin[authorityEnd..] != "/")
            throw new ValidationException("An embed origin must not contain a path, query, or fragment.");
    }
}
