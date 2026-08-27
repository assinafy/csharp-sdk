using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>Result of starting an ICP-Brasil digital-certificate signing session.</summary>
public sealed record CertificateStartResult
{
    /// <summary>Web PKI signing token to sign and pass to the completion endpoint.</summary>
    [JsonPropertyName("token")]
    public string Token { get; init; } = string.Empty;
}

/// <summary>Result of completing an ICP-Brasil digital-certificate signature.</summary>
public sealed record CertificateCompleteResult
{
    /// <summary>Name read from the certificate used to sign.</summary>
    [JsonPropertyName("signerName")]
    public string SignerName { get; init; } = string.Empty;
}

/// <summary>
/// One field value submitted by a signer when signing an assignment.
/// Per the Assinafy docs the Sign endpoint expects camelCase keys
/// (<c>itemId</c>, <c>fieldId</c>, <c>pageId</c>, <c>value</c>),
/// unlike the rest of the API which uses snake_case.
/// </summary>
public sealed class SignAssignmentValue
{
    /// <summary>The assignment item ID.</summary>
    [JsonPropertyName("itemId")]
    public required string ItemId { get; set; }

    /// <summary>ID of the field associated with the item.</summary>
    [JsonPropertyName("fieldId")]
    public required string FieldId { get; set; }

    /// <summary>The page ID.</summary>
    [JsonPropertyName("pageId")]
    public required string PageId { get; set; }

    /// <summary>String representation of the value entered for the item.</summary>
    [JsonPropertyName("value")]
    public required string Value { get; set; }
}

/// <summary>
/// Body for <c>PUT /signers/documents/sign-multiple</c>. Signs several documents in one
/// request; each must be prepared for the <c>virtual</c> method. Uses the signer access code.
/// </summary>
public sealed class SignMultipleDocumentsRequest
{
    /// <summary>IDs of the documents to sign.</summary>
    public required IReadOnlyList<string> DocumentIds { get; set; }
}

/// <summary>
/// Body for <c>PUT /signers/documents/decline-multiple</c>. Declines several documents
/// in one request. Uses the signer access code.
/// </summary>
public sealed class DeclineMultipleDocumentsRequest
{
    /// <summary>IDs of the documents to decline.</summary>
    public required IReadOnlyList<string> DocumentIds { get; set; }

    /// <summary>Reason for declining.</summary>
    public required string DeclineReason { get; set; }
}

/// <summary>
/// Body for <c>PUT /documents/{document_id}/assignments/{assignment_id}/reject</c>
/// (the signer declines to sign). Uses the signer access code.
/// </summary>
public sealed class DeclineAssignmentRequest
{
    /// <summary>Descriptive reason for declining.</summary>
    public required string DeclineReason { get; set; }
}

/// <summary>
/// Options shared by signer-document list and search methods. The published list contract accepts
/// <see cref="Page"/> and <see cref="PerPage"/>; search accepts <see cref="Search"/>. Other explicitly
/// set values are sent as compatibility extensions for older deployments.
/// </summary>
public sealed class SignerDocumentListParams
{
    /// <summary>Legacy status filter sent only when explicitly set.</summary>
    public string? Status { get; set; }

    /// <summary>Legacy assignment-method filter sent only when explicitly set.</summary>
    public string? Method { get; set; }

    /// <summary>Search term; documented for search and retained as a list compatibility extension.</summary>
    public string? Search { get; set; }

    /// <summary>Legacy sort expression sent only when explicitly set.</summary>
    public string? Sort { get; set; }

    /// <summary>1-based page number; documented for list and retained as a search compatibility extension.</summary>
    public int? Page { get; set; }

    /// <summary>Items per page; documented for list and retained as a search compatibility extension.</summary>
    public int? PerPage { get; set; }
}

/// <summary>Well-known signature image types accepted by the signature upload/download endpoints (the <c>type</c> query parameter).</summary>
public static class SignatureImageTypes
{
    /// <summary>The signer's full signature image.</summary>
    public const string Signature = "signature";

    /// <summary>The signer's initials image.</summary>
    public const string Initial = "initial";
}
