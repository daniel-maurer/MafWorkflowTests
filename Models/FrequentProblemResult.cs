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
}