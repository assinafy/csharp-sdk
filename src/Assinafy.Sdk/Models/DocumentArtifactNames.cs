namespace Assinafy.Sdk.Models;

/// <summary>Well-known artifact names accepted by the documents download endpoint.</summary>
public static class DocumentArtifactNames
{
    /// <summary>The original uploaded PDF.</summary>
    public const string Original = "original";

    /// <summary>The signed and certificated PDF (default for the download endpoint).</summary>
    public const string Certificated = "certificated";

    /// <summary>The standalone certificate page.</summary>
    public const string CertificatePage = "certificate-page";

    /// <summary>The signed PDF bundled with the certificate page.</summary>
    public const string Bundle = "bundle";
}
