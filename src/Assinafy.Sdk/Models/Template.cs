using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>A signer or editor role defined by a template. Supply one signer per role when creating a document from the template.</summary>
public sealed record TemplateRole
{
    /// <summary>The role's unique identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Human-readable role name (e.g. <c>Editor</c>).</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>The role's assignment type (e.g. <c>Editor</c>), or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("assignment_type")]
    public string? AssignmentType { get; init; }

    /// <summary>ISO-8601 date-time when the role was created, or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; init; }

    /// <summary>ISO-8601 date-time when the role was last updated, or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }
}

/// <summary>A field placed on a template page and bound to a role.</summary>
public sealed record TemplateFieldPlacement
{
    /// <summary>The placement's unique identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Identifier of the underlying field; matches the <c>field_id</c> used when creating a document from the template.</summary>
    [JsonPropertyName("field_id")]
    public string FieldId { get; init; } = string.Empty;

    /// <summary>Identifier of the <see cref="TemplateRole"/> this field is assigned to.</summary>
    [JsonPropertyName("role_id")]
    public string RoleId { get; init; } = string.Empty;

    /// <summary>Optional display label for the field.</summary>
    [JsonPropertyName("label")]
    public string? Label { get; init; }

    /// <summary>Rendering metadata for the placement, as raw JSON.</summary>
    [JsonPropertyName("display_settings")]
    public JsonElement? DisplaySettings { get; init; }

    /// <summary>ISO-8601 date-time when the placement was created, or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; init; }

    /// <summary>ISO-8601 date-time when the placement was last updated, or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }
}

/// <summary>A single page of a template, with its geometry and the fields placed on it.</summary>
public sealed record TemplatePage : PageBase
{
    /// <summary>The field placements positioned on this page.</summary>
    [JsonPropertyName("fields")]
    public IReadOnlyList<TemplateFieldPlacement> Fields { get; init; } = [];
}

/// <summary>
/// A template as returned by the list-templates endpoint
/// (<c>GET /accounts/{account_id}/templates</c>). Omits <c>default_document_tags</c>,
/// which only the single-template endpoint returns (see <see cref="TemplateDetails"/>).
/// </summary>
public record TemplateListItem
{
    /// <summary>Resource type discriminator; <c>template</c> for templates.</summary>
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    /// <summary>The template's unique identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>The template name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Default name for documents created from this template, or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("document_name")]
    public string? DocumentName { get; init; }

    /// <summary>Default invitation message sent to signers, or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>Processing status — one of <c>uploading</c>, <c>uploaded</c>, <c>processing</c>, <c>ready</c>, or <c>failed</c>.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>The template's pages, each with its field placements.</summary>
    [JsonPropertyName("pages")]
    public IReadOnlyList<TemplatePage> Pages { get; init; } = [];

    /// <summary>The roles defined by the template; supply one signer per role when creating a document.</summary>
    [JsonPropertyName("roles")]
    public IReadOnlyList<TemplateRole> Roles { get; init; } = [];

    /// <summary>Tags attached to the template itself.</summary>
    [JsonPropertyName("tags")]
    public IReadOnlyList<Tag> Tags { get; init; } = [];

    /// <summary>ISO-8601 date-time when the template was created.</summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;

    /// <summary>ISO-8601 date-time when the template was last updated, or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }
}

/// <summary>
/// Full template payload returned by the single-template endpoint (<c>GET
/// /accounts/{account_id}/templates/{template_id}</c>). Extends <see cref="TemplateListItem"/>
/// with <see cref="DefaultDocumentTags"/>, which the list endpoint does not return.
/// </summary>
public sealed record TemplateDetails : TemplateListItem
{
    /// <summary>Tags automatically applied to documents created from this template.</summary>
    [JsonPropertyName("default_document_tags")]
    public IReadOnlyList<Tag> DefaultDocumentTags { get; init; } = [];
}
