namespace Assinafy.Sdk.Models;

public sealed class SignAssignmentValue
{
    public required string ItemId { get; set; }
    public required string FieldId { get; set; }
    public required string PageId { get; set; }
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
