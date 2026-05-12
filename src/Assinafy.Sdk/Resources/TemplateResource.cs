using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Templates resource: list and inspect templates available to an account.</summary>
public sealed class TemplateResource : BaseResource
{
    internal TemplateResource(HttpClient http, string? defaultAccountId = null)
        : base(http, defaultAccountId) { }

    /// <summary><c>GET /accounts/{account_id}/templates</c> — list templates in a workspace with optional <c>status</c>, <c>search</c>, <c>sort</c> filters.</summary>
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
