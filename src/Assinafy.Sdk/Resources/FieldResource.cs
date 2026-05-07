using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Field definition management and signer/user field validation endpoints.</summary>
public sealed class FieldResource : BaseResource
{
    internal FieldResource(HttpClient http, string? defaultAccountId = null)
        : base(http, defaultAccountId) { }

    public Task<FieldDefinition> CreateAsync(
        CreateFieldDefinitionRequest request,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Type);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Name);

        var id = AccountId(accountId);
        return CallAsync<FieldDefinition>(
            $"accounts/{id}/fields",
            HttpMethod.Post,
            request,
            cancellationToken: cancellationToken);
    }

    public Task<PaginatedResult<FieldDefinition>> ListAsync(
        FieldListParams? parameters = null,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        return CallListAsync<FieldDefinition>(
            $"accounts/{id}/fields",
            BuildListQuery(parameters),
            cancellationToken);
    }

    public Task<FieldDefinition> GetAsync(
        string fieldId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var field = RequireId(fieldId, "Field ID");
        return CallAsync<FieldDefinition>(
            $"accounts/{id}/fields/{field}",
            HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    public Task<FieldDefinition> UpdateAsync(
        string fieldId,
        UpdateFieldDefinitionRequest request,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = AccountId(accountId);
        var field = RequireId(fieldId, "Field ID");

        return CallAsync<FieldDefinition>(
            $"accounts/{id}/fields/{field}",
            HttpMethod.Put,
            request,
            cancellationToken: cancellationToken);
    }

    public Task DeleteAsync(
        string fieldId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var field = RequireId(fieldId, "Field ID");

        return CallVoidAsync(
            $"accounts/{id}/fields/{field}",
            HttpMethod.Delete,
            cancellationToken: cancellationToken);
    }

    public Task<FieldValidationResult> ValidateAsync(
        string fieldId,
        ValidateFieldValueRequest request,
        string? signerAccessCode = null,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var id = AccountId(accountId);
        var field = RequireId(fieldId, "Field ID");

        var path = AppendQueryString(
            $"accounts/{id}/fields/{field}/validate",
            AccessCodeQuery(signerAccessCode));

        return CallAsync<FieldValidationResult>(
            path,
            HttpMethod.Post,
            request,
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<FieldValidationResult>> ValidateMultipleAsync(
        IReadOnlyList<ValidateFieldValueItem> values,
        string? signerAccessCode = null,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var id = AccountId(accountId);

        var path = AppendQueryString(
            $"accounts/{id}/fields/validate-multiple",
            AccessCodeQuery(signerAccessCode));

        var result = await CallAsync<List<FieldValidationResult>>(
            path,
            HttpMethod.Post,
            values,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result ?? [];
    }

    public async Task<IReadOnlyList<FieldTypeInfo>> ListTypesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await CallAsync<List<FieldTypeInfo>>(
            "field-types",
            HttpMethod.Get,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return result ?? [];
    }

    private static IDictionary<string, string?>? BuildListQuery(FieldListParams? parameters)
    {
        if (parameters is null) return null;

        var query = new Dictionary<string, string?>();
        if (parameters.IncludeInactive.HasValue)
            query["include_inactive"] = parameters.IncludeInactive.Value ? "true" : "false";
        if (parameters.IncludeStandard.HasValue)
            query["include_standard"] = parameters.IncludeStandard.Value ? "true" : "false";

        return query.Count > 0 ? query : null;
    }

    private static IDictionary<string, string?>? AccessCodeQuery(string? signerAccessCode)
    {
        return string.IsNullOrWhiteSpace(signerAccessCode)
            ? null
            : new Dictionary<string, string?> { ["signer-access-code"] = signerAccessCode };
    }
}
