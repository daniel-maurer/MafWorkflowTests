using System.Text.Json.Serialization;

namespace SupportWorkflow;

/// <summary>
/// Represents the result of analyzing a problem against known issues.
/// </summary>
public sealed class FrequentProblemResult
{
    /// <summary>
    /// Gets or sets whether the problem is known.
    /// </summary>
    [JsonPropertyName("is_known")]
    public bool IsKnown { get; set; }

    /// <summary>
    /// Gets or sets the message to display to the user about the resolution.
    /// </summary>
    [JsonPropertyName("message_for_user")]
    public string MessageForUser { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the problem is too complex and requires human intervention.
    /// </summary>
    [JsonPropertyName("is_complex")]
    public bool IsComplex { get; set; }

    /// <summary>
    /// Gets or sets the matched known issue (if found).
    /// </summary>
    [JsonPropertyName("matched_issue")]
    public KnownIssue? MatchedIssue { get; set; }

    /// <summary>
    /// Gets or sets the list of tools/actions that need to be executed for resolution.
    /// </summary>
    [JsonPropertyName("required_tools")]
    public List<string> RequiredTools { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the success rate of the resolution for this issue (0-1).
    /// </summary>
    [JsonPropertyName("success_rate")]
    public double SuccessRate { get; set; } = 0;
}