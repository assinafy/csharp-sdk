using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>A reusable field definition: a named, typed input (with optional regex validation) that can be placed on documents.</summary>
public sealed record FieldDefinition
{
    /// <summary>Resource type discriminator (<c>field</c>). Present in single-resource responses.</summary>
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    /// <summary>Unique field definition identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Display name of the field.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Field/validation type (e.g. <c>text</c>, <c>cpf</c>, <c>cnpj</c>, <c>signature</c>). See <c>GET /field-types</c> for the full list.</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>Optional regular expression used to validate input values, or <see langword="null"/> when the type has no custom pattern.</summary>
    [JsonPropertyName("regex")]
    public string? Regex { get; init; }

    /// <summary>Whether this is a system pre-defined field rather than a user-created one.</summary>
    [JsonPropertyName("is_pre_defined")]
    public bool IsPreDefined { get; init; }

    /// <summary>Whether the field definition is currently active (inactive definitions are hidden unless explicitly requested).</summary>
    [JsonPropertyName("is_active")]
    public bool IsActive { get; init; }

    /// <summary>Whether a value for this field is required.</summary>
    [JsonPropertyName("is_required")]
    public bool IsRequired { get; init; }

    /// <summary>Whether this is a standard field type (<c>signature</c>, <c>initial</c>, <c>signatureDate</c>).</summary>
    [JsonPropertyName("is_standard")]
    public bool IsStandard { get; init; }

    /// <summary>Whether the field is read-only.</summary>
    [JsonPropertyName("is_read_only")]
    public bool IsReadOnly { get; init; }

    /// <summary>Whether the field is visible.</summary>
    [JsonPropertyName("is_visible")]
    public bool IsVisible { get; init; }
}

/// <summary>A supported field/validation type, as returned by <c>GET /field-types</c>.</summary>
public sealed record FieldTypeInfo
{
    /// <summary>Type identifier (e.g. <c>cpf</c>, <c>cnpj</c>, <c>text</c>).</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>Human-readable name of the type (e.g. <c>CPF</c>).</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

/// <summary>The result of validating a value against a field definition, returned by both the single-field and multi-field validation endpoints.</summary>
public sealed record FieldValidationResult
{
    /// <summary>The field definition ID this result is for, or <see langword="null"/> for single-field validation responses.</summary>
    [JsonPropertyName("field_id")]
    public string? FieldId { get; init; }

    /// <summary>The field's validation type (e.g. <c>cpf</c>).</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>Whether the value passed validation.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>Validation error description; empty or <see langword="null"/> when the value is valid.</summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }
}

/// <summary>Body for <c>POST /accounts/{account_id}/fields</c> (create a field definition).</summary>
public sealed class CreateFieldDefinitionRequest
{
    /// <summary>Field/validation type (e.g. <c>text</c>, <c>cpf</c>, <c>cnpj</c>). See <c>GET /field-types</c> for the full list.</summary>
    public required string Type { get; set; }

    /// <summary>Display name of the field.</summary>
    public required string Name { get; set; }

    /// <summary>Optional regular expression used to validate input values.</summary>
    public string? Regex { get; set; }

    /// <summary>Whether a value for this field is required.</summary>
    public bool? IsRequired { get; set; }

    /// <summary>Whether the field definition should be active.</summary>
    public bool? IsActive { get; set; }
}

/// <summary>
/// Body for <c>PUT /accounts/{account_id}/fields/{field_id}</c>. Only non-null
/// properties are sent, so an unset property leaves the existing value unchanged.
/// </summary>
public sealed class UpdateFieldDefinitionRequest
{
    /// <summary>New field/validation type. See <c>GET /field-types</c> for the full list.</summary>
    public string? Type { get; set; }

    /// <summary>New display name.</summary>
    public string? Name { get; set; }

    /// <summary>New validation regular expression.</summary>
    public string? Regex { get; set; }

    /// <summary>Whether a value for this field is required.</summary>
    public bool? IsRequired { get; set; }

    /// <summary>Whether the field definition is active.</summary>
    public bool? IsActive { get; set; }
}

/// <summary>Optional filters for <c>Fields.ListAsync</c> (<c>GET /accounts/{account_id}/fields</c>).</summary>
public sealed class FieldListParams
{
    /// <summary>Include inactive field definitions in the results.</summary>
    public bool? IncludeInactive { get; set; }

    /// <summary>Include standard field types (<c>signature</c>, <c>initial</c>, <c>signatureDate</c>).</summary>
    public bool? IncludeStandard { get; set; }
}

/// <summary>Body for <c>POST /accounts/{account_id}/fields/{field_id}/validate</c> (validate a single value against a field definition).</summary>
public sealed class ValidateFieldValueRequest
{
    /// <summary>The input value to validate against the field definition.</summary>
    public required object Value { get; set; }
}

/// <summary>One <c>{field_id, value}</c> entry for <c>POST /accounts/{account_id}/fields/validate-multiple</c>.</summary>
public sealed class ValidateFieldValueItem
{
    /// <summary>The field definition ID to validate against.</summary>
    public required string FieldId { get; set; }

    /// <summary>The input value to validate.</summary>
    public required object Value { get; set; }
}
