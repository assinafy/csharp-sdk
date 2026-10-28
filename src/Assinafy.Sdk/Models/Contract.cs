using System.Text.Json;
using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>Shared contract or amendment identity. Inspect the runtime type returned by contract GET/PUT.</summary>
public abstract record ContractRecord
{
    /// <summary>Resource discriminator: document_contract or document_contract_amendment.</summary>
    [JsonPropertyName("resource")]
    public string Resource { get; init; } = string.Empty;

    /// <summary>Document carrying these terms or this amendment link.</summary>
    [JsonPropertyName("document_id")]
    public string DocumentId { get; init; } = string.Empty;

    /// <summary>Contract status (draft, active, expired, terminated), or amendment status (pending, applied).</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>ISO 4217 currency, or null without one. Applied amendments retain the currency at application.</summary>
    [JsonPropertyName("currency")]
    public string? Currency { get; init; }

    /// <summary>ISO-8601 creation timestamp.</summary>
    [JsonPropertyName("created_at")]
    public string CreatedAt { get; init; } = string.Empty;

    /// <summary>ISO-8601 change timestamp. Send this exact value in updated_at to reject stale writes.</summary>
    [JsonPropertyName("updated_at")]
    public string UpdatedAt { get; init; } = string.Empty;
}

/// <summary>Contract terms tracked independently of a document's signing lifecycle.</summary>
public sealed record DocumentContract : ContractRecord
{
    /// <summary>Counterparty display name, or null.</summary>
    [JsonPropertyName("counterparty_name")]
    public string? CounterpartyName { get; init; }

    /// <summary>Counterparty CPF or CNPJ, digits only, or null.</summary>
    [JsonPropertyName("counterparty_taxpayer_id")]
    public string? CounterpartyTaxpayerId { get; init; }

    /// <summary>Contract value with at most two decimal places, or null.</summary>
    [JsonPropertyName("value")]
    public decimal? Value { get; init; }

    /// <summary>Contract start date (YYYY-MM-DD), or null.</summary>
    [JsonPropertyName("start_date")]
    public string? StartDate { get; init; }

    /// <summary>Contract end date (YYYY-MM-DD), or null.</summary>
    [JsonPropertyName("end_date")]
    public string? EndDate { get; init; }

    /// <summary>Whether the lifecycle run renews the contract after its end date.</summary>
    [JsonPropertyName("auto_renew")]
    public bool AutoRenew { get; init; }

    /// <summary>Months in a renewal term (1–600); null uses 12.</summary>
    [JsonPropertyName("renewal_term_months")]
    public int? RenewalTermMonths { get; init; }

    /// <summary>Days before the end date for renewal notice (0–3650), or null for no notice.</summary>
    [JsonPropertyName("notice_period_days")]
    public int? NoticePeriodDays { get; init; }

    /// <summary>Current term, starting at 1 and incremented by renewal or an end-date change.</summary>
    [JsonPropertyName("term")]
    public int Term { get; init; }

    /// <summary>Responsible workspace member, or null. Lifecycle emails follow this member's preferences.</summary>
    [JsonPropertyName("owner_id")]
    public string? OwnerId { get; init; }

    /// <summary>Up to 50 custom text values (500 characters each), or null.</summary>
    [JsonPropertyName("metadata")]
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>External system/ID pairs, ordered by system and ID.</summary>
    [JsonPropertyName("external_references")]
    public IReadOnlyList<ContractExternalReference> ExternalReferences { get; init; } = [];

    /// <summary>Document embedded by expand=document on the contract list, or null.</summary>
    [JsonPropertyName("document")]
    public DocumentDetails? Document { get; init; }

    /// <summary>Linked instruments on single/related reads; applied instruments by number, then pending ones.</summary>
    [JsonPropertyName("amendments")]
    public IReadOnlyList<DocumentContractAmendment>? Amendments { get; init; }
}

/// <summary>A signed or pending instrument that amends, renews, or terminates a parent contract.</summary>
public sealed record DocumentContractAmendment : ContractRecord
{
    /// <summary>Instrument document's name.</summary>
    [JsonPropertyName("document_name")]
    public string DocumentName { get; init; } = string.Empty;

    /// <summary>Root contract document whose terms this instrument changes.</summary>
    [JsonPropertyName("parent_document_id")]
    public string ParentDocumentId { get; init; } = string.Empty;

    /// <summary>Instrument type: amendment, renewal, or termination.</summary>
    [JsonPropertyName("relation_type")]
    public string RelationType { get; init; } = string.Empty;

    /// <summary>Position in applied signing order; null while pending.</summary>
    [JsonPropertyName("number")]
    public int? Number { get; init; }

    /// <summary>Instrument's own document status. Application requires certificated.</summary>
    [JsonPropertyName("document_status")]
    public string DocumentStatus { get; init; } = string.Empty;

    /// <summary>Reason an instrument cannot apply yet; null when none is reported.</summary>
    [JsonPropertyName("apply_error")]
    public string? ApplyError { get; init; }

    /// <summary>Only the changed contract terms, or null. Values follow DocumentContractInput.</summary>
    [JsonPropertyName("terms")]
    public IReadOnlyDictionary<string, JsonElement>? Terms { get; init; }

    /// <summary>Per-field from/to values: preview while pending, immutable record once applied.</summary>
    [JsonPropertyName("changes")]
    public IReadOnlyDictionary<string, ContractFieldChange>? Changes { get; init; }

    /// <summary>ISO-8601 application timestamp, or null while pending.</summary>
    [JsonPropertyName("applied_at")]
    public string? AppliedAt { get; init; }
}

/// <summary>An external system's identifier for a contract.</summary>
public sealed record ContractExternalReference
{
    /// <summary>System name; letters, digits, underscore, dash or dot, up to 64 characters; stored lowercase.</summary>
    [JsonPropertyName("system")]
    public required string System { get; init; }

    /// <summary>External identifier, up to 255 characters; no four-byte Unicode characters.</summary>
    [JsonPropertyName("id")]
    public required string Id { get; init; }
}

/// <summary>A contract field's values before and after an instrument applies.</summary>
public sealed record ContractFieldChange
{
    /// <summary>Previous value, including JSON null.</summary>
    [JsonPropertyName("from")]
    public JsonElement From { get; init; }

    /// <summary>New value, including JSON null.</summary>
    [JsonPropertyName("to")]
    public JsonElement To { get; init; }
}
