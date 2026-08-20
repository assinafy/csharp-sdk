using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Templates resource: list and inspect templates available to an account.</summary>
public sealed class TemplateResource : BaseResource
{
    internal TemplateResource(HttpClient http, string? defaultAccountId = null, Action<HttpRequestMessage>? authenticate = null)
        : base(http, defaultAccountId, authenticate) { }

    /// <summary><c>GET /accounts/{account_id}/templates</c> — list templates in a workspace with optional <c>search</c>, <c>page</c>, and <c>per-page</c> filters.</summary>
    /// <param name="queryParams">Optional <c>search</c>, <c>page</c>, and <c>per-page</c> query filters.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<PaginatedResult<TemplateListItem>> ListAsync(
        IDictionary<string, string?>? queryParams = null,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        return CallListAsync<TemplateListItem>(
            $"accounts/{id}/templates",
            queryParams,
            cancellationToken);
    }

    /// <summary><c>GET /accounts/{account_id}/templates/{template_id}</c> — fetch template details including roles, pages, and field placements.</summary>
    /// <param name="templateId">Template to fetch.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<TemplateDetails> GetAsync(
        string templateId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var template = RequireId(templateId, "Template ID");
        return CallAsync<TemplateDetails>(
            $"accounts/{id}/templates/{template}",
            HttpMethod.Get,
            cancellationToken: cancellationToken);
    }
}
