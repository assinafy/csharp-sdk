using System.Text.Json;

namespace Assinafy.Sdk.Exceptions;

/// <summary>
/// Thrown when a failure carries a machine-readable OAuth 2.1 error code, either from the flat
/// <c>{ error, error_description }</c> body of an OAuth endpoint or from a
/// <c>WWW-Authenticate: Bearer</c> challenge on any API endpoint. Derives from
/// <see cref="ApiException"/>, so an existing <c>catch (ApiException)</c> still handles it;
/// catch this type instead to branch on <see cref="Error"/>.
/// </summary>
/// <remarks>
/// The codes that drive integration logic are <c>invalid_grant</c> (the authorization code or
/// refresh token is spent, expired, or from another client — send the user through the
/// authorization flow again rather than retrying), <c>invalid_client</c> (wrong credentials or a
/// disabled application), and <c>insufficient_scope</c> (the token is valid but was never granted
/// the permission named by <see cref="Scope"/> — reconnect requesting it, do not retry).
/// </remarks>
public sealed class OAuthException : ApiException
{
    /// <summary>The OAuth error code, for example <c>invalid_grant</c> or <c>insufficient_scope</c>.</summary>
    public string Error { get; }

    /// <summary>Human-readable description supplied with the error, or <see langword="null"/> when none was returned.</summary>
    public string? ErrorDescription { get; }

    /// <summary>
    /// The scope named by an <c>insufficient_scope</c> challenge — the permission to request on the
    /// next authorization round-trip — or <see langword="null"/> for errors that name no scope.
    /// </summary>
    public string? Scope { get; }

    /// <summary>Creates a new <see cref="OAuthException"/>.</summary>
    /// <param name="statusCode">HTTP status code reported with the error.</param>
    /// <param name="error">The OAuth error code.</param>
    /// <param name="errorDescription">Optional human-readable description of the error.</param>
    /// <param name="scope">Optional scope named by an <c>insufficient_scope</c> challenge.</param>
    /// <param name="details">Optional raw error payload.</param>
    public OAuthException(
        int statusCode,
        string error,
        string? errorDescription = null,
        string? scope = null,
        JsonElement? details = null)
        : base(statusCode, errorDescription ?? error, details)
    {
        Error = error;
        ErrorDescription = errorDescription;
        Scope = scope;
    }
}
