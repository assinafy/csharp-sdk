using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>
/// A workspace tag. Tags are unique per workspace (case-insensitive),
/// can carry an optional hex color, and can be attached to documents and templates.
/// </summary>
public sealed record Tag
{
    /// <summary>Resource type discriminator (<c>tag</c>). Present in single-resource responses.</summary>
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    /// <summary>Unique tag identifier.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Tag name (unique per workspace, case-insensitive).</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Six-character hex color (without the leading <c>#</c>), or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("color")]
    public string? Color { get; init; }

    /// <summary>When the tag was created (ISO-8601 date-time string).</summary>
    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; init; }

    /// <summary>When the tag was last updated (ISO-8601 date-time string).</summary>
    [JsonPropertyName("updated_at")]
    public string? UpdatedAt { get; init; }
}

/// <summary>Body for <c>POST /accounts/{account_id}/tags</c>.</summary>
public sealed class CreateTagRequest
{
    /// <summary>Tag name (max 64 characters). Unique per workspace, case-insensitive.</summary>
    public required string Name { get; set; }

    /// <summary>Optional six-character hex color, with or without a leading <c>#</c>.</summary>
    public string? Color { get; set; }
}

/// <summary>
/// Body for <c>PUT /accounts/{account_id}/tags/{tag_id}</c>. Only non-null
/// properties are sent, so an unset property leaves the existing value unchanged.
/// </summary>
public sealed class UpdateTagRequest
{
    /// <summary>New tag name (max 64 characters). Unique per workspace, case-insensitive.</summary>
    public string? Name { get; set; }

    /// <summary>Optional six-character hex color, with or without a leading <c>#</c>.</summary>
    public string? Color { get; set; }

    /// <summary>When <see langword="true"/>, clear the existing color by sending <c>"color": null</c>. Cannot be combined with <see cref="Color"/>.</summary>
    [JsonIgnore]
    public bool ClearColor { get; set; }
}

/// <summary>Response from deleting a workspace tag.</summary>
public sealed record DeleteTagResult
{
    /// <summary>Whether the tag was deleted.</summary>
    [JsonPropertyName("deleted")]
    public bool Deleted { get; init; }
}

/// <summary>Response from detaching a tag from a document.</summary>
public sealed record DetachTagResult
{
    /// <summary>Whether the tag was detached.</summary>
    [JsonPropertyName("detached")]
    public bool Detached { get; init; }
}
