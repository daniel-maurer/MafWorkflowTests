using System.Text.Json.Serialization;

namespace SalesWorkflow.Models;

public sealed class SalesResolutionResult
{
    [JsonPropertyName("is_resolved")]
    public bool IsResolved { get; set; }

    [JsonPropertyName("requires_human")]
    public bool RequiresHuman { get; set; }

    [JsonPropertyName("resolution_type")]
    public string ResolutionType { get; set; } = string.Empty;

    [JsonPropertyName("message_for_user")]
    public string MessageForUser { get; set; } = string.Empty;

    [JsonPropertyName("quote_id")]
    public string? QuoteId { get; set; }

    [JsonPropertyName("escalation_reason")]
    public string? EscalationReason { get; set; }

    [JsonPropertyName("actions_executed")]
    public List<string> ActionsExecuted { get; set; } = [];
}
