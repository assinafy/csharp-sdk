using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>
/// One field value submitted by a signer when signing an assignment.
/// Per the Assinafy docs the Sign endpoint expects camelCase keys
/// (<c>itemId</c>, <c>fieldId</c>, <c>pageId</c>, <c>value</c>),
/// unlike the rest of the API which uses snake_case.
/// </summary>
public sealed class SignAssignmentValue
{
    [JsonPropertyName("itemId")]
    public required string ItemId { get; set; }

    [JsonPropertyName("fieldId")]
    public required string FieldId { get; set; }

    [JsonPropertyName("pageId")]
    public required string PageId { get; set; }

    [JsonPropertyName("value")]
    public required string Value { get; set; }
}

public sealed class SignMultipleDocumentsRequest
{
    public required IReadOnlyList<string> DocumentIds { get; set; }
}

public sealed class DeclineMultipleDocumentsRequest
{
    public required IReadOnlyList<string> DocumentIds { get; set; }
    public required string DeclineReason { get; set; }
}

public sealed class DeclineAssignmentRequest
{
    public required string DeclineReason { get; set; }
}

public sealed class SignerDocumentListParams
{
    public string? Status { get; set; }
    public string? Method { get; set; }
    public string? Search { get; set; }
    public string? Sort { get; set; }
    public int? Page { get; set; }
    public int? PerPage { get; set; }
}

public static class SignatureImageTypes
{
    public const string Signature = "signature";
    public const string Initial = "initial";
}
