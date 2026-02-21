namespace SupportWorkflow;

/// <summary>
/// Contains constants used throughout the support workflow system.
/// </summary>
public static class Constants
{
    /// <summary>
    /// The state scope key for storing triage-related conversation state.
    /// </summary>
    public const string TriageStateScope = "TriageState";

    /// <summary>
    /// The state key for storing conversation history.
    /// </summary>
    public const string ConversationHistoryKey = "conversation_history";

    /// <summary>
    /// The state key for storing the problem summary.
    /// </summary>
    public const string ProblemSummaryKey = "problem_summary";

    /// <summary>
    /// Maximum number of iterations allowed in iterative agent workflows to prevent infinite loops.
    /// </summary>
    public const int MaxWorkflowIterations = 5;
}
