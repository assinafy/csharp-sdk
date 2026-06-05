using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

public sealed record TemplateRole
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("assignment_type")]
    public string? AssignmentType { get; init; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }
}

public sealed record TemplateFieldPlacement
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("field_id")]
    public string FieldId { get; init; } = string.Empty;

    [JsonPropertyName("role_id")]
    public string RoleId { get; init; } = string.Empty;

    [JsonPropertyName("label")]
    public string? Label { get; init; }

    [JsonPropertyName("display_settings")]
    public JsonElement? DisplaySettings { get; init; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; init; }

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }
}

public sealed record TemplatePage : PageBase
{
    [JsonPropertyName("fields")]
    public IReadOnlyList<TemplateFieldPlacement> Fields { get; init; } = [];
}

public record TemplateListItem
{
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("document_name")]
    public string? DocumentName { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    [JsonPropertyName("pages")]
    public IReadOnlyList<TemplatePage> Pages { get; init; } = [];

    [JsonPropertyName("roles")]
    public IReadOnlyList<TemplateRole> Roles { get; init; } = [];

    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;

    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }
}

/// <summary>
/// Full template payload returned by the single-template endpoint. Currently shares the
/// shape of <see cref="TemplateListItem"/>; kept as a distinct type for forward-compatible
/// detail-only fields.
/// </summary>
public sealed record TemplateDetails : TemplateListItem;
