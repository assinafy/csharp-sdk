using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Assinafy.Sdk.Support;

/// <summary>
/// Reads the auth-params out of an RFC 6750 <c>WWW-Authenticate: Bearer</c> challenge, which is
/// how the API reports an OAuth token that is missing a scope:
/// <c>Bearer error="insufficient_scope", scope="documents:write", resource_metadata="..."</c>.
/// </summary>
/// <remarks>
/// The raw header text is matched rather than <see cref="HttpResponseHeaders.WwwAuthenticate"/>,
/// because the typed collection splits a single challenge on the commas separating its auth-params
/// and reports each fragment as its own scheme.
/// </remarks>
internal static partial class BearerChallenge
{
    private static readonly Dictionary<string, string> NoParameters =
        new(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex("(?<key>[A-Za-z_]+)\\s*=\\s*\"(?<value>[^\"]*)\"", RegexOptions.ExplicitCapture)]
    private static partial Regex ParameterPattern();

    /// <summary>
    /// Extract the auth-params of the <c>Bearer</c> challenge on a response, or an empty
    /// dictionary when the response carries no such challenge.
    /// </summary>
    /// <param name="response">Response whose <c>WWW-Authenticate</c> headers are inspected.</param>
    /// <returns>Auth-params keyed by name, compared case-insensitively.</returns>
    public static IReadOnlyDictionary<string, string> Parse(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("WWW-Authenticate", out var values))
            return NoParameters;

        var header = string.Join(", ", values);
        if (!header.Contains("Bearer", StringComparison.OrdinalIgnoreCase))
            return NoParameters;

        Dictionary<string, string>? parameters = null;
        foreach (Match match in ParameterPattern().Matches(header))
        {
            parameters ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            parameters[match.Groups["key"].Value] = match.Groups["value"].Value;
        }

        return parameters ?? NoParameters;
    }
}
