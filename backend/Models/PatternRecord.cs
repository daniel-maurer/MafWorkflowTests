using System.Text.Json.Serialization;

namespace SupportWorkflow;

/// <summary>
/// Represents a simplified detected pattern in user issues that could become a known issue.
/// </summary>
public class PatternRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the description of the identified pattern.
    /// </summary>
    [JsonPropertyName("pattern_description")]
    public string PatternDescription { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets how many times this pattern has been detected.
    /// </summary>
    [JsonPropertyName("frequency")]
    public int Frequency { get; set; } = 1;

    /// <summary>
    /// Gets or sets the date when this pattern was first detected.
    /// </summary>
    [JsonPropertyName("first_detected")]
    public DateTime FirstDetected { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the date when this pattern was last detected.
    /// </summary>
    [JsonPropertyName("last_detected")]
    public DateTime LastDetected { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets example symptoms for this pattern.
    /// </summary>
    [JsonPropertyName("example_symptoms")]
    public List<string> ExampleSymptoms { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets example solutions that worked for this pattern.
    /// </summary>
    [JsonPropertyName("example_solutions")]
    public List<string> ExampleSolutions { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets whether this pattern has been promoted to a known issue.
    /// </summary>
    [JsonPropertyName("promoted_to_known_issue")]
    public bool PromotedToKnownIssue { get; set; }

    /// <summary>
    /// Gets or sets the ID of the known issue if promoted (references KnownIssue.Problem).
    /// </summary>
    [JsonPropertyName("linked_known_issue")]
    public string? LinkedKnownIssue { get; set; }
}
