using System.Text.Json.Serialization;

namespace Assinafy.Sdk.Models;

/// <summary>Workspace plan and usage, without billing prices or payment details.</summary>
public sealed record AccountUsage
{
    /// <summary>Plan name; null when the plan is absent or retired.</summary>
    [JsonPropertyName("plan")]
    public string? Plan { get; init; }

    /// <summary>Next document-allowance renewal (YYYY-MM-DD); null while awaiting first payment or without a billing day.</summary>
    [JsonPropertyName("documents_renewal_date")]
    public string? DocumentsRenewalDate { get; init; }

    /// <summary>Document and credit balances.</summary>
    [JsonPropertyName("balances")]
    public AccountBalances Balances { get; init; } = new();
}

/// <summary>The workspace's document and credit allowances.</summary>
public sealed record AccountBalances
{
    /// <summary>Document allowance.</summary>
    [JsonPropertyName("documents")]
    public UsageBalance Documents { get; init; } = new();

    /// <summary>Credit allowance.</summary>
    [JsonPropertyName("credits")]
    public UsageBalance Credits { get; init; } = new();
}

/// <summary>A document or credit balance for the current billing cycle.</summary>
public sealed record UsageBalance
{
    /// <summary>Remaining quantity; null means unlimited.</summary>
    [JsonPropertyName("balance")]
    public decimal? Balance { get; init; }

    /// <summary>Allowance per cycle or pool; null when unlimited or not cycle-limited.</summary>
    [JsonPropertyName("limit")]
    public decimal? Limit { get; init; }

    /// <summary>Quantity consumed in the current cycle.</summary>
    [JsonPropertyName("spent")]
    public decimal Spent { get; init; }
}

/// <summary>Workspace notification rules set by its owners.</summary>
public sealed record NotificationPolicy
{
    /// <summary>Whether the requesting user can change the policy.</summary>
    [JsonPropertyName("editable")]
    public bool Editable { get; init; }

    /// <summary>Whether owners also receive push alerts for other members' documents, subject to their own preferences.</summary>
    [JsonPropertyName("owners_receive_all")]
    public bool OwnersReceiveAll { get; init; }

    /// <summary>Exact event codes mapped to enabled flags. Unknown future event keys are retained.</summary>
    [JsonPropertyName("events")]
    public IReadOnlyDictionary<string, bool> Events { get; init; } = new Dictionary<string, bool>();
}

/// <summary>Partial changes to the workspace notification policy.</summary>
public sealed class UpdateNotificationPolicyRequest
{
    /// <summary>Only event codes to change; false disables the workspace event on all channels, except mandatory identity review email.</summary>
    public IReadOnlyDictionary<string, bool>? Events { get; set; }

    /// <summary>Whether owners receive all members' document push alerts; null leaves this unchanged.</summary>
    public bool? OwnersReceiveAll { get; set; }
}
