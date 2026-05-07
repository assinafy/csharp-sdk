using System.Text.RegularExpressions;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Account-scoped signer management and signer self-service endpoints.</summary>
public sealed partial class SignerResource : BaseResource
{
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();

    internal SignerResource(HttpClient http, string? defaultAccountId = null)
        : base(http, defaultAccountId) { }

    public Task<Signer> CreateAsync(
        CreateSignerRequest request,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FullName);
        AssertOptionalEmail(request.Email);

        var id = AccountId(accountId);
        return CallAsync<Signer>(
            $"accounts/{id}/signers",
            HttpMethod.Post,
            request,
            cancellationToken: cancellationToken);
    }

    public Task<Signer> GetAsync(
        string signerId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var signer = RequireId(signerId, "Signer ID");
        return CallAsync<Signer>($"accounts/{id}/signers/{signer}", HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    public Task<PaginatedResult<Signer>> ListAsync(
        IDictionary<string, string?>? queryParams = null,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        return CallListAsync<Signer>($"accounts/{id}/signers", queryParams, cancellationToken);
    }

    public Task<Signer> UpdateAsync(
        string signerId,
        UpdateSignerRequest request,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        AssertOptionalEmail(request.Email);

        var id = AccountId(accountId);
        var signer = RequireId(signerId, "Signer ID");
        return CallAsync<Signer>(
            $"accounts/{id}/signers/{signer}",
            HttpMethod.Put,
            request,
            cancellationToken: cancellationToken);
    }

    public Task DeleteAsync(
        string signerId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var signer = RequireId(signerId, "Signer ID");
        return CallVoidAsync($"accounts/{id}/signers/{signer}", HttpMethod.Delete,
            cancellationToken: cancellationToken);
    }

    public async Task<Signer?> FindByEmailAsync(
        string email,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        AssertEmail(email);
        var id = AccountId(accountId);

        var result = await CallListAsync<Signer>(
            $"accounts/{id}/signers",
            new Dictionary<string, string?> { ["search"] = email, ["per-page"] = "100" },
            cancellationToken).ConfigureAwait(false);

        return result.Data.FirstOrDefault(s =>
            string.Equals(s.Email, email, StringComparison.OrdinalIgnoreCase));
    }

    public Task<Signer> GetSelfAsync(
        string signerAccessCode,
        CancellationToken cancellationToken = default)
    {
        var code = RequireId(signerAccessCode, "Signer access code");
        var path = AppendQueryString("signers/self", new Dictionary<string, string?>
        {
            ["signer-access-code"] = code,
        });

        return CallAsync<Signer>(path, HttpMethod.Get, cancellationToken: cancellationToken);
    }

    public Task<Signer> AcceptTermsAsync(
        string signerAccessCode,
        CancellationToken cancellationToken = default)
    {
        var code = RequireId(signerAccessCode, "Signer access code");
        return CallAsync<Signer>(
            "signers/accept-terms",
            HttpMethod.Put,
            new Dictionary<string, object?> { ["signer-access-code"] = code },
            cancellationToken: cancellationToken);
    }

    public Task<VerifyEmailResult> VerifyEmailAsync(
        string signerAccessCode,
        string verificationCode,
        CancellationToken cancellationToken = default)
    {
        var accessCode = RequireId(signerAccessCode, "Signer access code");
        var code = RequireId(verificationCode, "Verification code");

        return CallAsync<VerifyEmailResult>(
            "verify",
            HttpMethod.Post,
            new Dictionary<string, object?>
            {
                ["signer-access-code"] = accessCode,
                ["verification-code"] = code,
            },
            cancellationToken: cancellationToken);
    }

    public Task ConfirmDataAsync(
        string documentId,
        string signerAccessCode,
        ConfirmSignerDataRequest request,
        CancellationToken cancellationToken = default)
    {
        var document = RequireId(documentId, "Document ID");
        var code = RequireId(signerAccessCode, "Signer access code");
        ArgumentNullException.ThrowIfNull(request);
        AssertOptionalEmail(request.Email);

        var path = AppendQueryString(
            $"documents/{document}/signers/confirm-data",
            new Dictionary<string, string?> { ["signer-access-code"] = code });

        return CallVoidAsync(path, HttpMethod.Put, request, cancellationToken);
    }

    private static void AssertOptionalEmail(string? email)
    {
        if (!string.IsNullOrWhiteSpace(email))
            AssertEmail(email);
    }

    private static void AssertEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !EmailRegex().IsMatch(email))
            throw new ValidationException("Invalid email address.");
    }
}
