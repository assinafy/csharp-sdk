using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

public sealed record FieldDefinition
{
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("regex")]
    public string? Regex { get; init; }

    [JsonPropertyName("is_pre_defined")]
    public bool IsPreDefined { get; init; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; init; }

    [JsonPropertyName("is_required")]
    public bool IsRequired { get; init; }

    [JsonPropertyName("is_standard")]
    public bool IsStandard { get; init; }

    [JsonPropertyName("is_read_only")]
    public bool IsReadOnly { get; init; }

    [JsonPropertyName("is_visible")]
    public bool IsVisible { get; init; }
}

public sealed record FieldTypeInfo
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}

public sealed record FieldValidationResult
{
    [JsonPropertyName("field_id")]
    public string? FieldId { get; init; }

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("success")]
    public bool Success { get; init; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; init; }
}

public sealed class CreateFieldDefinitionRequest
{
    public required string Type { get; set; }
    public required string Name { get; set; }
    public string? Regex { get; set; }
    public bool? IsRequired { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class UpdateFieldDefinitionRequest
{
    public string? Type { get; set; }
    public string? Name { get; set; }
    public string? Regex { get; set; }
    public bool? IsRequired { get; set; }
    public bool? IsActive { get; set; }
}

public sealed class FieldListParams
{
    public bool? IncludeInactive { get; set; }
    public bool? IncludeStandard { get; set; }
}

public sealed class ValidateFieldValueRequest
{
    public required object Value { get; set; }
}

public sealed class ValidateFieldValueItem
{
    public required string FieldId { get; set; }
    public required object Value { get; set; }
}
