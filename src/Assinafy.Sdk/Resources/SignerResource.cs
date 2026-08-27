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
    /// <returns>The newly created signer.</returns>
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
    /// <returns>The requested signer.</returns>
    public Task<Signer> GetAsync(
        string signerId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var signer = PathSegment(signerId, "Signer ID");
        return CallAsync<Signer>($"accounts/{id}/signers/{signer}", HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /accounts/{account_id}/signers</c> — list signers with optional <c>search</c>, <c>page</c>, and <c>per-page</c> filters.</summary>
    /// <param name="queryParams">Optional filters: <c>search</c> (partial name or email match), <c>page</c> (1-based), and <c>per-page</c> (page size).</param>
    /// <param name="accountId">Workspace account whose signers to list; falls back to the client default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A paginated collection of signers.</returns>
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
    /// <returns>The updated signer.</returns>
    public Task<Signer> UpdateAsync(
        string signerId,
        UpdateSignerRequest request,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        AssertOptionalEmail(request.Email);

        var id = AccountId(accountId);
        var signer = PathSegment(signerId, "Signer ID");
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
    /// <returns>A task that completes when the signer has been removed.</returns>
    public Task DeleteAsync(
        string signerId,
        string? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var id = AccountId(accountId);
        var signer = PathSegment(signerId, "Signer ID");
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
        var seenPages = new HashSet<string>(StringComparer.Ordinal);
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

            var pageFingerprint = string.Join(
                '\n',
                result.Data.Select(signer => $"{signer.Id}\t{signer.Email}"));
            var lastPage = result.Meta?.LastPage;
            if (lastPage is null && result.Meta is { Total: int total, PerPage: > 0 } meta)
                lastPage = (total + meta.PerPage!.Value - 1) / meta.PerPage.Value;

            if (result.Data.Count == 0 ||
                !seenPages.Add(pageFingerprint) ||
                lastPage is not null && page >= lastPage)
                return null;

            page++;
        }
    }

    /// <summary><c>GET /signers/self</c> — signer-facing endpoint: load the signer's own profile using only an access code.</summary>
    /// <param name="signerAccessCode">The signer's per-assignment access code (issued with the signing link) that authorizes this self-service call.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The signer's profile and current signing-state flags.</returns>
    public Task<Signer> GetSelfAsync(
        string signerAccessCode,
        CancellationToken cancellationToken = default)
    {
        var code = RequireId(signerAccessCode, "Signer access code");
        var path = AppendQueryString("signers/self", AccessCodeQuery(code));

        return CallAsync<Signer>(
            path,
            HttpMethod.Get,
            cancellationToken: cancellationToken,
            authenticate: false);
    }

    /// <summary><c>PUT /signers/accept-terms</c> — record acceptance of the terms. The API returns no data; the returned <see cref="Signer"/> is a synthetic legacy-compatibility result.</summary>
    /// <param name="signerAccessCode">The signer's per-assignment access code (issued with the signing link) that authorizes this self-service call.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A compatibility result with <see cref="Signer.HasAcceptedTerms"/> set. The current operation returns no data.</returns>
    public async Task<Signer> AcceptTermsAsync(
        string signerAccessCode,
        CancellationToken cancellationToken = default)
    {
        var code = RequireId(signerAccessCode, "Signer access code");
        var path = AppendQueryString("signers/accept-terms", AccessCodeQuery(code));

        await CallVoidAsync(
            path,
            HttpMethod.Put,
            cancellationToken: cancellationToken,
            authenticate: false).ConfigureAwait(false);

        return new Signer { HasAcceptedTerms = true };
    }

    /// <summary><c>POST /verify</c> — verify an email or WhatsApp one-time code. The API returns no data.</summary>
    /// <param name="signerAccessCode">The signer's per-assignment access code (issued with the signing link) that authorizes this self-service call.</param>
    /// <param name="verificationCode">The one-time verification code delivered to the signer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the one-time code has been verified.</returns>
    public Task VerifyAsync(
        string signerAccessCode,
        string verificationCode,
        CancellationToken cancellationToken = default)
    {
        var accessCode = RequireId(signerAccessCode, "Signer access code");
        var code = RequireId(verificationCode, "Verification code");
        var path = AppendQueryString("verify", AccessCodeQuery(accessCode));

        return CallVoidAsync(
            path,
            HttpMethod.Post,
            new Dictionary<string, object?> { ["verification-code"] = code },
            cancellationToken,
            authenticate: false);
    }

    /// <summary><c>POST /verify</c> — verify an emailed OTP. The API returns no data; the returned <see cref="VerifyEmailResult"/> is a synthetic legacy result.</summary>
    /// <param name="signerAccessCode">The signer's per-assignment access code (issued with the signing link) that authorizes this self-service call.</param>
    /// <param name="verificationCode">The one-time code that was emailed to the signer.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A compatibility result reporting successful verification. The current operation returns no data.</returns>
    public async Task<VerifyEmailResult> VerifyEmailAsync(
        string signerAccessCode,
        string verificationCode,
        CancellationToken cancellationToken = default)
    {
        await VerifyAsync(signerAccessCode, verificationCode, cancellationToken).ConfigureAwait(false);

        return new VerifyEmailResult
        {
            IsEmailVerified = true,
        };
    }

    /// <summary>
    /// <c>PUT /documents/{document_id}/signers/confirm-data</c> — signer-facing endpoint:
    /// confirm or supply the signer's full name, email, and government ID for a virtual assignment.
    /// Virtual assignments require this call to succeed before <see cref="SigningResource.SignAsync"/>.
    /// </summary>
    /// <param name="documentId">Document the signer is party to.</param>
    /// <param name="signerAccessCode">The signer's per-assignment access code (issued with the signing link) that authorizes this self-service call.</param>
    /// <param name="request">The full name, email, and/or government ID to confirm; when present, <c>Email</c> must be a valid address.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes after the signer data is confirmed. Use <see cref="ConfirmDataWithResultAsync"/> to receive the updated signer payload.</returns>
    public async Task ConfirmDataAsync(
        string documentId,
        string signerAccessCode,
        ConfirmSignerDataRequest request,
        CancellationToken cancellationToken = default)
    {
        _ = await ConfirmDataWithResultAsync(
            documentId,
            signerAccessCode,
            request,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary><c>PUT /documents/{document_id}/signers/confirm-data</c> — confirm signer data and return the complete updated signer payload.</summary>
    /// <param name="documentId">Document the signer is party to.</param>
    /// <param name="signerAccessCode">Signer access code.</param>
    /// <param name="request">Signer values to confirm.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated signer returned by the API.</returns>
    public Task<Signer> ConfirmDataWithResultAsync(
        string documentId,
        string signerAccessCode,
        ConfirmSignerDataRequest request,
        CancellationToken cancellationToken = default)
    {
        var document = PathSegment(documentId, "Document ID");
        var code = RequireId(signerAccessCode, "Signer access code");
        ArgumentNullException.ThrowIfNull(request);
        AssertOptionalEmail(request.Email);

        var path = AppendQueryString(
            $"documents/{document}/signers/confirm-data",
            AccessCodeQuery(code));

        var body = new Dictionary<string, object?>();
        if (request.FullName is not null) body["full_name"] = request.FullName;
        if (request.Email is not null) body["email"] = request.Email;
        if (request.GovernmentId is not null) body["government_id"] = request.GovernmentId;
        if (request.WhatsAppPhoneNumber is not null)
            body["whatsapp_phone_number"] = request.WhatsAppPhoneNumber;
        if (request.HasAcceptedTerms.HasValue)
            body["has_accepted_terms"] = request.HasAcceptedTerms.Value;

        return CallAsync<Signer>(
            path,
            HttpMethod.Put,
            body,
            cancellationToken: cancellationToken,
            authenticate: false);
    }

    private static void AssertOptionalEmail(string? email)
    {
        if (email is not null)
            AssertEmail(email);
    }

    private static void AssertEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !EmailRegex().IsMatch(email))
            throw new ValidationException("Invalid email address.");
    }
}
