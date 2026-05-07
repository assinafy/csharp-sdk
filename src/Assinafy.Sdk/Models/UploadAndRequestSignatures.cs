namespace Assinafy.Sdk.Models;

public sealed class UploadAndRequestSignaturesSigner
{
    public required string FullName { get; set; }
    public string? Email { get; set; }
    public string? WhatsAppPhoneNumber { get; set; }
}

public sealed class UploadAndRequestSignaturesOptions
{
    public required Stream FileStream { get; set; }
    public required string FileName { get; set; }
    public required IReadOnlyList<UploadAndRequestSignaturesSigner> Signers { get; set; }
    public bool? WaitForReady { get; set; }
    public string? Message { get; set; }
    public string? ExpiresAt { get; set; }
    public string[]? CopyReceivers { get; set; }
    public string? AccountId { get; set; }
}

public sealed class UploadAndRequestSignaturesResult
{
    public required DocumentDetails Document { get; init; }
    public required Assignment Assignment { get; init; }
    public required IReadOnlyList<string> SignerIds { get; init; }
}
