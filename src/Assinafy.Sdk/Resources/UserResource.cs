using System.Text.Json;
using Assinafy.Sdk.Exceptions;
using Assinafy.Sdk.Models;

namespace Assinafy.Sdk.Resources;

/// <summary>
/// Authenticated-user profile, notification-preference, and cross-account KPI endpoints.
/// </summary>
public sealed class UserResource : BaseResource
{
    internal UserResource(HttpClient http, Action<HttpRequestMessage>? authenticate = null)
        : base(http, authenticate: authenticate) { }

    /// <summary><c>GET /users/self</c> — retrieve the authenticated user's profile.</summary>
    /// <param name="cancellationToken">Cancellation token.</param>
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
}
