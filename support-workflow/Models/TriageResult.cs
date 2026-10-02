using System.Text.Json.Serialization;

namespace SupportWorkflow;

/// <summary>
/// Represents the result of the triage process where user requests are analyzed and classified.
/// </summary>
public sealed class TriageResult
{
    /// <summary>
    /// Gets or sets whether the triage agent understood the user's problem.
    /// If false, QuestionForUser contains a clarifying question.
    /// </summary>
    [JsonPropertyName("is_understood")]
    public bool IsUnderstood { get; set; }
    
    /// <summary>
    /// Gets or sets a clarifying question if IsUnderstood is false.
    /// </summary>
    [JsonPropertyName("question_for_user")]
    public string QuestionForUser { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the summary of the user's problem.
    /// </summary>
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the urgency level of the issue (critical, high, medium, low).
    /// </summary>
    [JsonPropertyName("urgency")]
    public string Urgency { get; set; } = string.Empty;
}