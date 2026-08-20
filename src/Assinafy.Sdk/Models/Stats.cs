using System.Globalization;
using System.Text.Json.Serialization;
using Assinafy.Sdk.Exceptions;

namespace Assinafy.Sdk.Models;

/// <summary>Well-known values for <see cref="DocumentStatsParams.Granularity"/>.</summary>
public static class DocumentStatsGranularities
{
    /// <summary>Return one row per month for the latest 12 months.</summary>
    public const string Monthly = "monthly";

    /// <summary>Return one row per day in <see cref="DocumentStatsParams.Month"/>.</summary>
    public const string Daily = "daily";
}

/// <summary>Optional query parameters for account and cross-account document KPI endpoints.</summary>
public sealed class DocumentStatsParams
{
    /// <summary>
    /// Result grouping: <see cref="DocumentStatsGranularities.Monthly"/> (the API default)
    /// or <see cref="DocumentStatsGranularities.Daily"/>.
    /// </summary>
    public string? Granularity { get; set; }

    /// <summary>
    /// Target month in <c>YYYY-MM</c> format. Required by the API when
    /// <see cref="Granularity"/> is <see cref="DocumentStatsGranularities.Daily"/>.
    /// </summary>
    public string? Month { get; set; }

    internal IDictionary<string, string?>? ToQueryParameters()
    {
        if (Granularity is not null &&
            Granularity != DocumentStatsGranularities.Monthly &&
            Granularity != DocumentStatsGranularities.Daily)
            throw new ValidationException("Stats granularity must be 'monthly' or 'daily'.");

        if (Month is not null &&
            !DateTime.TryParseExact(Month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new ValidationException("Stats month must use YYYY-MM format.");

        if (Granularity == DocumentStatsGranularities.Daily && string.IsNullOrWhiteSpace(Month))
            throw new ValidationException("Stats month is required for daily granularity.");

        var query = new Dictionary<string, string?>();
        if (!string.IsNullOrWhiteSpace(Granularity)) query["granularity"] = Granularity;
        if (!string.IsNullOrWhiteSpace(Month)) query["month"] = Month;
        return query.Count == 0 ? null : query;
    }
}

/// <summary>
/// One period in the account or cross-account document KPI series. Monthly series use
/// <c>YYYY-MM</c> periods; daily series use <c>YYYY-MM-DD</c> periods.
/// </summary>
public sealed record DocumentStatsRow
{
    /// <summary>Period represented by this row: <c>YYYY-MM</c> or <c>YYYY-MM-DD</c>.</summary>
    [JsonPropertyName("period")]
    public string Period { get; init; } = string.Empty;

    /// <summary>Documents uploaded during the period.</summary>
    [JsonPropertyName("documents_uploaded")]
    public int DocumentsUploaded { get; init; }

    /// <summary>Documents sent for signature during the period.</summary>
    [JsonPropertyName("documents_sent")]
    public int DocumentsSent { get; init; }

    /// <summary>Total signature requests created during the period.</summary>
    [JsonPropertyName("signature_requests")]
    public int SignatureRequests { get; init; }

    /// <summary>Signature requests sent by email during the period.</summary>
    [JsonPropertyName("signature_requests_email")]
    public int SignatureRequestsEmail { get; init; }

    /// <summary>Signature requests sent by WhatsApp during the period.</summary>
    [JsonPropertyName("signature_requests_whatsapp")]
    public int SignatureRequestsWhatsapp { get; init; }

    /// <summary>Signature requests whose document was first viewed during the period.</summary>
    [JsonPropertyName("signature_requests_viewed")]
    public int SignatureRequestsViewed { get; init; }

    /// <summary>Signature requests completed by individual signers during the period.</summary>
    [JsonPropertyName("signature_requests_completed")]
    public int SignatureRequestsCompleted { get; init; }

    /// <summary>Documents certified during the period.</summary>
    [JsonPropertyName("documents_certified")]
    public int DocumentsCertified { get; init; }
}
