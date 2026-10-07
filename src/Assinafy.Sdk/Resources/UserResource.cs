using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>
/// Authenticated-user profile, two-factor authentication, notification-preference, and cross-account KPI endpoints.
/// </summary>
public sealed class UserResource : BaseResource
{
    internal UserResource(HttpClient http, Action<HttpRequestMessage>? authenticate = null)
        : base(http, authenticate: authenticate) { }

    /// <summary><c>GET /users/self</c> — retrieve the authenticated user's profile.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The authenticated user's profile.</returns>
    /// <remarks>
    /// Supports both the production response, whose envelope data is the user object directly,
    /// and the sandbox's legacy envelope data containing <c>{ user, accounts }</c>.
    /// </remarks>
    public async Task<UserProfile> GetSelfAsync(CancellationToken cancellationToken = default)
    {
        var data = await CallAsync<JsonElement>(
            "users/self",
            HttpMethod.Get,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        var userData = data.ValueKind == JsonValueKind.Object &&
                       data.TryGetProperty("user", out var legacyUser)
            ? legacyUser
            : data;

        try
        {
            if (userData.ValueKind != JsonValueKind.Object)
                throw new JsonException("The user payload is not a JSON object.");

            var user = userData.Deserialize<UserProfile>(JsonOptions);
            if (user is null || string.IsNullOrWhiteSpace(user.Id))
                throw new JsonException("The user payload is missing its required id.");

            return user;
        }
        catch (JsonException ex)
        {
            throw new SerializationException("The API response did not contain a valid authenticated user.", ex);
        }
    }

    /// <summary><c>GET /users/self/notification-preferences</c> — retrieve all nine email notification preferences.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The authenticated user's notification preferences.</returns>
    public Task<NotificationPreferences> GetNotificationPreferencesAsync(
        CancellationToken cancellationToken = default)
    {
        return CallAsync<NotificationPreferences>(
            "users/self/notification-preferences",
            HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>PUT /users/self/notification-preferences</c> — merge selected email notification preferences and return the full updated map.</summary>
    /// <param name="request">Preferences to change; null properties are omitted and keep their existing values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The complete updated notification-preference map.</returns>
    public Task<NotificationPreferences> UpdateNotificationPreferencesAsync(
        UpdateNotificationPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.DocumentCompleted is null &&
            request.SignerDeclined is null &&
            request.DocumentCancelled is null &&
            request.DocumentAboutToExpire is null &&
            request.DocumentExpired is null &&
            request.DocumentExpirationReset is null &&
            request.DocumentProcessingFailed is null &&
            request.TemplateProcessingFailed is null &&
            request.SignerWhatsappFailed is null)
            throw new ValidationException("At least one notification preference is required.");

        return CallAsync<NotificationPreferences>(
            "users/self/notification-preferences",
            HttpMethod.Put,
            request,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>GET /users/self/stats</c> — retrieve document KPIs summed across all accounts the authenticated user belongs to.</summary>
    /// <param name="parameters">Optional monthly or daily grouping and target month.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The document KPI rows for the requested grouping.</returns>
    public async Task<IReadOnlyList<DocumentStatsRow>> GetStatsAsync(
        DocumentStatsParams? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var path = AppendQueryString("users/self/stats", parameters?.ToQueryParameters());
        return await CallListBodyAsync<DocumentStatsRow>(
            path,
            HttpMethod.Get,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary><c>GET /users/self/mfa</c> — list the authenticated user's enrolled two-factor methods and how many recovery codes remain.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The enrolled methods and the unused recovery-code count.</returns>
    public Task<MfaMethodList> ListMfaMethodsAsync(CancellationToken cancellationToken = default)
    {
        return CallAsync<MfaMethodList>(
            "users/self/mfa",
            HttpMethod.Get,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// <c>POST /users/self/mfa/totp</c> — create an unconfirmed authenticator method and return its shared secret,
    /// which no other call returns. Two-factor authentication stays off until <see cref="ConfirmTotpEnrollmentAsync"/>.
    /// </summary>
    /// <param name="request">Optional device label; <see langword="null"/> sends an empty body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The method ID, base32 secret, and <c>otpauth://</c> provisioning URI.</returns>
    public Task<TotpEnrollment> StartTotpEnrollmentAsync(
        StartTotpEnrollmentRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        return CallAsync<TotpEnrollment>(
            "users/self/mfa/totp",
            HttpMethod.Post,
            request ?? new StartTotpEnrollmentRequest(),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// <c>PUT /users/self/mfa/totp/confirm</c> — activate an enrollment with a live code from the new device. From
    /// then on every login requires a second factor. Replacing an existing confirmed method also requires
    /// <see cref="ConfirmTotpEnrollmentRequest.Password"/> or <see cref="ConfirmTotpEnrollmentRequest.ReauthCode"/>
    /// and reissues the recovery codes.
    /// </summary>
    /// <param name="request">The enrollment ID, the new device's code, and re-authentication when replacing a method.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The recovery codes, shown only once.</returns>
    public Task<MfaRecoveryCodes> ConfirmTotpEnrollmentAsync(
        ConfirmTotpEnrollmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Id);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Code);

        return CallAsync<MfaRecoveryCodes>(
            "users/self/mfa/totp/confirm",
            HttpMethod.Put,
            request,
            cancellationToken: cancellationToken);
    }

    /// <summary><c>POST /users/self/mfa/recovery-codes</c> — issue ten fresh recovery codes and invalidate the previous set.</summary>
    /// <param name="request">The current password, a live authenticator code, or an existing recovery code (which is consumed).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The new recovery codes, shown only once.</returns>
    public Task<MfaRecoveryCodes> RegenerateRecoveryCodesAsync(
        MfaReauthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        AssertReauthentication(request);
        return CallAsync<MfaRecoveryCodes>(
            "users/self/mfa/recovery-codes",
            HttpMethod.Post,
            request,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// <c>DELETE /users/self/mfa/{method_id}</c> — remove an enrolled two-factor method. Removing the last method
    /// also discards the recovery codes.
    /// </summary>
    /// <param name="methodId">The <see cref="MfaMethod.Id"/> to remove.</param>
    /// <param name="request">The current password, a live authenticator code, or an existing recovery code (which is consumed).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Whether two-factor authentication remains enabled.</returns>
    public Task<MfaMethodRemoval> DeleteMfaMethodAsync(
        string methodId,
        MfaReauthenticationRequest request,
        CancellationToken cancellationToken = default)
    {
        AssertReauthentication(request);
        return CallAsync<MfaMethodRemoval>(
            $"users/self/mfa/{PathSegment(methodId, "MFA method ID")}",
            HttpMethod.Delete,
            request,
            cancellationToken: cancellationToken);
    }

    private static void AssertReauthentication(MfaReauthenticationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Password) && string.IsNullOrWhiteSpace(request.Code))
            throw new ValidationException("A password or a two-factor code is required.");
    }
}
