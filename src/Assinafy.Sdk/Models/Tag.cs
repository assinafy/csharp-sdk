using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>
/// A workspace tag. Tags are unique per workspace (case-insensitive),
/// can carry an optional hex color, and can be attached to documents and templates.
/// </summary>
public sealed record Tag
{
    [JsonPropertyName("resource")]
    public string? Resource { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Six-character hex color (without the leading <c>#</c>), or <see langword="null"/> when unset.</summary>
    [JsonPropertyName("color")]
    public string? Color { get; init; }

    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; init; }

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
    public string? Name { get; set; }

    /// <summary>Optional six-character hex color, with or without a leading <c>#</c>.</summary>
    public string? Color { get; set; }
}
