using System.Text.RegularExpressions;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>Account-scoped signer management and signer self-service endpoints.</summary>
public sealed partial class SignerResource : BaseResource
{
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();

    internal SignerResource(HttpClient http, string? defaultAccountId = null, Action<HttpRequestMessage>? authenticate = null)
        : base(http, defaultAccountId, authenticate) { }

    /// <summary><c>POST /accounts/{account_id}/signers</c> — create a signer within the workspace.</summary>
    /// <param name="request">New signer details; <c>FullName</c> is required and, when present, <c>Email</c> must be a valid address.</param>
    /// <param name="accountId">Workspace account to create the signer in; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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

    /// <summary><c>GET /accounts/{account_id}/signers/{signer_id}</c> — fetch a single signer's profile.</summary>
    /// <param name="signerId">Signer to fetch.</param>
    /// <param name="accountId">Workspace account that owns the signer; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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

    /// <summary><c>GET /accounts/{account_id}/signers</c> — list signers with optional <c>search</c>, <c>sort</c>, <c>page</c>, <c>per-page</c> filters.</summary>
    /// <param name="queryParams">Optional filters. Accepted keys: <c>search</c> (partial match on the signer's full name or email); <c>sort</c>; <c>page</c> (1-based page number); and <c>per-page</c> (page size).</param>
    /// <param name="accountId">Workspace account whose signers to list; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<PaginatedResult<Signer>> ListAsync(
        IDictionary<string, string?>? queryParams = null,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        return CallListAsync<Signer>($"accounts/{id}/signers", queryParams, cancellationToken);
    }

    /// <summary><c>PUT /accounts/{account_id}/signers/{signer_id}</c> — update a signer. Verification integrity rules may block changing email or WhatsApp phone for in-flight signers.</summary>
    /// <param name="signerId">Signer to update.</param>
    /// <param name="request">Fields to update; when present, <c>Email</c> must be a valid address.</param>
    /// <param name="accountId">Workspace account that owns the signer; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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

    /// <summary><c>DELETE /accounts/{account_id}/signers/{signer_id}</c> — remove a signer from the workspace.</summary>
    /// <param name="signerId">Signer to remove.</param>
    /// <param name="accountId">Workspace account that owns the signer; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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

    /// <summary>
    /// Convenience helper: page through <see cref="ListAsync"/> filtered by the email
    /// (a server-side fuzzy <c>search</c>) and return the first exact, case-insensitive
    /// email match across all result pages, or <see langword="null"/> if none exists.
    /// </summary>
    /// <param name="email">Exact email address to match (case-insensitive); also used as the server-side search term.</param>
    /// <param name="accountId">Workspace account to search; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching signer, or <see langword="null"/> if none exists.</returns>
    public async Task<Signer?> FindByEmailAsync(
        string email,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        AssertEmail(email);
        var id = AccountId(accountId);

        var page = 1;
        while (true)
        {
            var result = await CallListAsync<Signer>(
                $"accounts/{id}/signers",
                new Dictionary<string, string?>
                {
                    ["search"] = email,
                    ["per-page"] = "100",
                    ["page"] = page.ToString(),
                },
                cancellationToken).ConfigureAwait(false);

            var match = result.Data.FirstOrDefault(s =>
                string.Equals(s.Email, email, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;

            var lastPage = result.Meta?.LastPage ?? 1;
            if (result.Data.Count == 0 || page >= lastPage) return null;

            page++;
        }
    }

    /// <summary><c>GET /signers/self</c> — signer-facing endpoint: load the signer's own profile using only an access code.</summary>
    /// <param name="signerAccessCode">The signer's per-assignment access code (issued with the signing link) that authorizes this self-service call.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<Signer> GetSelfAsync(
        string signerAccessCode,
        CancellationToken cancellationToken = default)
    {
        var code = RequireId(signerAccessCode, "Signer access code");
        var path = AppendQueryString("signers/self", AccessCodeQuery(code));

        return CallAsync<Signer>(path, HttpMethod.Get, cancellationToken: cancellationToken);
    }

    /// <summary><c>PUT /signers/accept-terms</c> — signer-facing endpoint: record acceptance of the terms of use.</summary>
    /// <param name="signerAccessCode">The signer's per-assignment access code (issued with the signing link) that authorizes this self-service call.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<Signer> AcceptTermsAsync(
        string signerAccessCode,
        CancellationToken cancellationToken = default)
    {
        var code = RequireId(signerAccessCode, "Signer access code");
        return CallAsync<Signer>(
            "signers/accept-terms",
            HttpMethod.Put,
            new Dictionary<string, object?> { [SignerAccessCodeParam] = code },
            cancellationToken: cancellationToken);
    }

    /// <summary><c>POST /verify</c> — signer-facing endpoint: verify the OTP code emailed to the signer.</summary>
    /// <param name="signerAccessCode">The signer's per-assignment access code (issued with the signing link) that authorizes this self-service call.</param>
    /// <param name="verificationCode">The one-time code that was emailed to the signer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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
                [SignerAccessCodeParam] = accessCode,
                ["verification-code"] = code,
            },
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// <c>PUT /documents/{document_id}/signers/confirm-data</c> — signer-facing endpoint:
    /// confirm or supply email / WhatsApp number for a virtual assignment, optionally accepting terms.
    /// Virtual assignments require this call to succeed before <see cref="SigningResource.SignAsync"/>.
    /// </summary>
    /// <param name="documentId">Document the signer is party to.</param>
    /// <param name="signerAccessCode">The signer's per-assignment access code (issued with the signing link) that authorizes this self-service call.</param>
    /// <param name="request">The email and/or WhatsApp number to confirm, plus optional terms acceptance; when present, <c>Email</c> must be a valid address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
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
            AccessCodeQuery(code));

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
