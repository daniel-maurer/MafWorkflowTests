using System.Text.Json.Serialization;

namespace SupportWorkflow;

/// <summary>
/// Represents the result of pattern analysis by the pattern record agent.
/// </summary>
public sealed class PatternRecordResult
{
    /// <summary>
    /// Gets or sets the type of pattern detected (e.g., Temporal, Benefit, Payment).
    /// </summary>
    [JsonPropertyName("pattern_type")]
    public string PatternType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a clear description of the identified pattern.
    /// </summary>
    [JsonPropertyName("pattern_description")]
    public string PatternDescription { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the keywords associated with this pattern.
    /// </summary>
    [JsonPropertyName("keywords")]
    public List<string> Keywords { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the temporal characteristics if applicable (e.g., "Usually day 3").
    /// </summary>
    [JsonPropertyName("temporal_info")]
    public string? TemporalInfo { get; set; }

    /// <summary>
    /// Gets or sets example symptoms that indicate this pattern.
    /// </summary>
    [JsonPropertyName("example_symptoms")]
    public List<string> ExampleSymptoms { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the solution or steps to resolve this pattern.
    /// </summary>
    [JsonPropertyName("solution")]
    public string Solution { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the estimated time to resolve this pattern.
    /// </summary>
    [JsonPropertyName("estimated_resolution_time")]
    public string? EstimatedResolutionTime { get; set; }

    /// <summary>
    /// Gets or sets the estimated success rate of the solution (0-1).
    /// </summary>
    [JsonPropertyName("success_rate")]
    public double SuccessRate { get; set; } = 0;

    /// <summary>
    /// Gets or sets the estimated frequency of this pattern (how often it occurs).
    /// </summary>
    [JsonPropertyName("estimated_frequency")]
    public string? EstimatedFrequency { get; set; }

    /// <summary>
    /// Gets or sets the severity level of this pattern.
    /// </summary>
    [JsonPropertyName("severity")]
    public string Severity { get; set; } = "Medium";

    /// <summary>
    /// Gets or sets whether this pattern is ready for automation.
    /// </summary>
    [JsonPropertyName("ready_for_automation")]
    public bool ReadyForAutomation { get; set; } = false;

    /// <summary>
    /// Gets or sets additional notes about the pattern.
    /// </summary>
    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}
