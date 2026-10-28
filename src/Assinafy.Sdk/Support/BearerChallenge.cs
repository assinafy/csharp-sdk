using System.Net.Http.Headers;
using System.Text;

namespace Assinafy.Sdk.Support;

/// <summary>
/// Reads the auth-params out of an RFC 6750 <c>WWW-Authenticate: Bearer</c> challenge, which is
/// how the API reports an OAuth token that is missing a scope:
/// <c>Bearer error="insufficient_scope", scope="documents:write", resource_metadata="..."</c>.
/// </summary>
/// <remarks>
/// The raw header text is parsed rather than <see cref="HttpResponseHeaders.WwwAuthenticate"/>,
/// because the typed collection splits a single challenge on the commas separating its auth-params
/// and reports each fragment as its own scheme.
/// </remarks>
internal static class BearerChallenge
{
    private static readonly Dictionary<string, string> NoParameters =
        new(StringComparer.OrdinalIgnoreCase);

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
        Dictionary<string, string>? parameters = null;
        var start = 0;
        var quoted = false;
        for (var i = 0; i <= header.Length; i++)
        {
            if (i < header.Length)
            {
                if (quoted && header[i] == '\\' && i + 1 < header.Length)
                {
                    i++;
                    continue;
                }
                if (header[i] == '"') quoted = !quoted;
                if (header[i] != ',' || quoted) continue;
            }

            var part = header[start..i].Trim();
            start = i + 1;
            if (part.Length == 0) continue;

            if (!NameValueHeaderValue.TryParse(part, out var parameter) || parameter.Value is null)
            {
                // A new scheme ends the first Bearer challenge; its parameters cannot overwrite it.
                if (parameters is not null) break;
                if (!AuthenticationHeaderValue.TryParse(part, out var challenge) ||
                    !string.Equals(challenge.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase))
                    continue;

                parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (!NameValueHeaderValue.TryParse(challenge.Parameter, out parameter) || parameter.Value is null)
                    continue;
            }

            if (parameters is not null)
                parameters[parameter.Name] = Unquote(parameter.Value);
        }

        return parameters ?? NoParameters;
    }

    private static string Unquote(string value)
    {
        if (value.Length == 0 || value[0] != '"') return value;

        var result = new StringBuilder(value.Length - 2);
        for (var i = 1; i < value.Length - 1; i++)
        {
            if (value[i] == '\\') i++;
            result.Append(value[i]);
        }
        return result.ToString();
    }
}
