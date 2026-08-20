using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Field definition management and signer/user field validation endpoints.</summary>
public sealed class FieldResource : BaseResource
{
    internal FieldResource(HttpClient http, string? defaultAccountId = null, Action<HttpRequestMessage>? authenticate = null)
        : base(http, defaultAccountId, authenticate) { }

    /// <summary><c>POST /accounts/{accountId}/fields</c> — create a workspace-scoped field definition.</summary>
    /// <param name="request">Field definition to create (type and name are required).</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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

    /// <summary><c>GET /accounts/{accountId}/fields</c> — list field definitions. Use <see cref="FieldListParams"/> to include inactive or standard built-ins.</summary>
    /// <param name="parameters">Optional filters to include inactive or standard built-in fields.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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

    /// <summary><c>GET /accounts/{accountId}/fields/{field_id}</c> — fetch a single field definition.</summary>
    /// <param name="fieldId">Field definition to fetch.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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

    /// <summary><c>PUT /accounts/{account_id}/fields/{field_id}</c> — update a field definition.</summary>
    /// <param name="fieldId">Field definition to update.</param>
    /// <param name="request">New field-definition values.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<FieldDefinition> UpdateAsync(
        string fieldId,
        UpdateFieldDefinitionRequest request,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.ClearRegex && request.Regex is not null)
            throw new ValidationException("Regex and ClearRegex cannot both be set.");

        var id = AccountId(accountId);
        var field = RequireId(fieldId, "Field ID");

        return CallAsync<FieldDefinition>(
            $"accounts/{id}/fields/{field}",
            HttpMethod.Put,
            BuildUpdatePayload(request),
            cancellationToken: cancellationToken);
    }

    /// <summary><c>DELETE /accounts/{account_id}/fields/{field_id}</c> — delete a field definition. Will fail if the field has already been used on a document.</summary>
    /// <param name="fieldId">Field definition to delete.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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

    /// <summary><c>POST /accounts/{accountId}/fields/{field_id}/validate</c> — validate a single value against a field definition. Pass a signer access code when calling on a signer's behalf.</summary>
    /// <param name="fieldId">Field definition to validate against.</param>
    /// <param name="request">The value to validate.</param>
    /// <param name="signerAccessCode">Optional signer access code; supply it when validating on a signer's behalf.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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
            OptionalAccessCodeQuery(signerAccessCode));

        return CallAsync<FieldValidationResult>(
            path,
            HttpMethod.Post,
            request,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>POST /accounts/{accountId}/fields/validate-multiple</c> — validate multiple values at once.</summary>
    /// <param name="values">Field-value pairs to validate; each references its field definition.</param>
    /// <param name="signerAccessCode">Optional signer access code; supply it when validating on a signer's behalf.</param>
    /// <param name="accountId">Workspace (account) ID; falls back to the client's configured default when <see langword="null"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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
            OptionalAccessCodeQuery(signerAccessCode));

        return await CallListBodyAsync<FieldValidationResult>(
            path,
            HttpMethod.Post,
            values,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary><c>GET /field-types</c> — list the platform-supported field input types.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<IReadOnlyList<FieldTypeInfo>> ListTypesAsync(
        CancellationToken cancellationToken = default)
    {
        return await CallListBodyAsync<FieldTypeInfo>(
            "field-types",
            HttpMethod.Get,
            cancellationToken: cancellationToken).ConfigureAwait(false);
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

    private static Dictionary<string, object?> BuildUpdatePayload(UpdateFieldDefinitionRequest request)
    {
        var body = new Dictionary<string, object?>();
        if (request.Name is not null) body["name"] = request.Name;
        if (request.ClearRegex) body["regex"] = null;
        else if (request.Regex is not null) body["regex"] = request.Regex;
        if (request.IsActive.HasValue) body["is_active"] = request.IsActive.Value;

        // Compatibility extensions supported by older deployments.
        if (request.Type is not null) body["type"] = request.Type;
        if (request.IsRequired.HasValue) body["is_required"] = request.IsRequired.Value;
        return body;
    }

    private static IDictionary<string, string?>? OptionalAccessCodeQuery(string? signerAccessCode)
    {
        return string.IsNullOrWhiteSpace(signerAccessCode)
            ? null
            : new Dictionary<string, string?> { [SignerAccessCodeParam] = signerAccessCode };
    }
}
