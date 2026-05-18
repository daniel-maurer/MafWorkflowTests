using System.Text.Json.Serialization;

namespace SupportWorkflow;

/// <summary>
/// Represents the result of attempting to resolve a support issue.
/// </summary>
public sealed class ResolutionResult
{
    /// <summary>
    /// Gets or sets whether the issue was resolved successfully.
    /// </summary>
    [JsonPropertyName("is_resolved")]
    public bool IsResolved { get; set; }

    /// <summary>
    /// Gets or sets whether the issue requires human intervention.
    /// </summary>
    [JsonPropertyName("requires_human")]
    public bool RequiresHuman { get; set; }

    /// <summary>
    /// Gets or sets the message to display to the user about the resolution outcome.
    /// </summary>
    [JsonPropertyName("message_for_user")]
    public string MessageForUser { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the actions that were executed during resolution.
    /// </summary>
    [JsonPropertyName("actions_executed")]
    public List<string> ActionsExecuted { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the reason why human intervention is needed (if applicable).
    /// </summary>
    [JsonPropertyName("escalation_reason")]
    public string? EscalationReason { get; set; }
}
