using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Assinafy.Sdk.Webhooks;

/// <summary>
/// Verifies the <a href="https://www.standardwebhooks.com">Standard Webhooks</a> signature Assinafy
/// attaches to deliveries from an endpoint with signing enabled.
/// </summary>
public static class WebhookSignature
{
    private const string SecretPrefix = "whsec_";

    /// <summary>The replay window applied when no tolerance is given: five minutes either side of the local clock.</summary>
    public static readonly TimeSpan DefaultTolerance = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Returns <see langword="true"/> when a delivery was signed with <paramref name="secret"/> and its
    /// timestamp falls within <paramref name="tolerance"/> of the current time.
    /// </summary>
    /// <param name="secret">The endpoint's signing secret (<c>whsec_…</c>) from <c>GetEndpointSecretAsync</c>.</param>
    /// <param name="webhookId">The <c>webhook-id</c> request header.</param>
    /// <param name="webhookTimestamp">The <c>webhook-timestamp</c> request header (Unix seconds).</param>
    /// <param name="signatureHeader">The <c>webhook-signature</c> request header: one or more space-separated <c>v1,&lt;base64&gt;</c> entries.</param>
    /// <param name="rawBody">The request body exactly as received. Do not re-serialize parsed JSON.</param>
    /// <param name="tolerance">Maximum clock difference accepted; <see cref="DefaultTolerance"/> when <see langword="null"/>.</param>
    /// <param name="timeProvider">Clock used for the replay check; <see cref="TimeProvider.System"/> when <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when any <c>v1</c> signature matches and the timestamp is fresh; otherwise <see langword="false"/>, including when a header is missing or malformed.</returns>
    /// <exception cref="ArgumentException"><paramref name="secret"/> is empty or not base64 after the <c>whsec_</c> prefix.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="rawBody"/> is <see langword="null"/>.</exception>
    public static bool Verify(
        string secret,
        string? webhookId,
        string? webhookTimestamp,
        string? signatureHeader,
        string rawBody,
        TimeSpan? tolerance = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentNullException.ThrowIfNull(rawBody);

        var encodedKey = secret.StartsWith(SecretPrefix, StringComparison.Ordinal) ? secret[SecretPrefix.Length..] : secret;
        byte[] key;
        try
        {
            key = Convert.FromBase64String(encodedKey);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException("The webhook secret is not a valid whsec_ base64 key.", nameof(secret), ex);
        }

        if (string.IsNullOrEmpty(webhookId) || string.IsNullOrEmpty(signatureHeader) ||
            !long.TryParse(webhookTimestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds))
            return false;

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow().ToUnixTimeSeconds();
        if (Math.Abs(now - seconds) > (long)(tolerance ?? DefaultTolerance).TotalSeconds)
            return false;

        var expected = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes($"{webhookId}.{webhookTimestamp}.{rawBody}"));
        Span<byte> candidate = stackalloc byte[expected.Length];

        foreach (var entry in signatureHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (entry.StartsWith("v1,", StringComparison.Ordinal) &&
                Convert.TryFromBase64String(entry[3..], candidate, out var written) &&
                written == expected.Length &&
                CryptographicOperations.FixedTimeEquals(candidate, expected))
                return true;
        }

        return false;
    }
}
