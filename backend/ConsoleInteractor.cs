namespace SupportWorkflow;

/// <summary>
/// Represents a single tool invocation produced by an agent, surfaced inline next to its chat message.
/// </summary>
public sealed class AgentToolCall
{
    public string Name { get; init; } = string.Empty;
    public string Args { get; init; } = string.Empty;
    public bool Ok { get; init; } = true;
}

/// <summary>
/// Information surfaced to the frontend's KB panel.
/// </summary>
public sealed class KbEntry
{
    public string Id { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public double Score { get; init; }
    public string Summary { get; init; } = string.Empty;
    public string ResolutionType { get; init; } = string.Empty;
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Visibility scope for a chat message.
/// "both"      — shown in both the client and attendant panes (default).
/// "client"    — shown only on the client side (single chat or user pane in split mode).
/// "attendant" — shown only on the attendant/human-agent pane in split mode.
/// "internal"  — never rendered as a chat bubble; useful for summaries that should only
///               appear in the trace tab.
/// </summary>
public static class MessageAudience
{
    public const string Both = "both";
    public const string Client = "client";
    public const string Attendant = "attendant";
    public const string Internal = "internal";
}

/// <summary>
/// Special token enqueued into the session message channel when the frontend clicks
/// "Mark as Solved". Executors waiting on user/human input observe this and resolve
/// the current step gracefully (no terminal interaction required).
/// </summary>
internal static class WorkflowControlTokens
{
    public const string MarkResolved = "__MAF_CONTROL__:mark-resolved";
    public const string Cancel = "__MAF_CONTROL__:cancel";
}

/// <summary>
/// Handles user-facing interaction for the support workflow (chat, KB, traces, agent state, etc.).
/// </summary>
internal interface IUserInteractor
{
    Task<string> GetUserResponseAsync(string prompt, string? agentId = null, IReadOnlyList<AgentToolCall>? tools = null, string audience = MessageAudience.Both, CancellationToken cancellationToken = default);
    Task SendUserResponseAsync(string prompt, string? agentId = null, IReadOnlyList<AgentToolCall>? tools = null, string audience = MessageAudience.Both, CancellationToken cancellationToken = default);
    Task SendSystemMessageAsync(string text, string systemStyle = "handoff", string audience = MessageAudience.Both, CancellationToken cancellationToken = default);
    Task SetAgentTypingAsync(string label, bool on, CancellationToken cancellationToken = default);
    Task PublishTraceAsync(string title, string level = "info", CancellationToken cancellationToken = default);
    Task PublishAgentStateAsync(string agentId, string state, string tag, CancellationToken cancellationToken = default);
    Task PublishContextAsync(string status, string chatTitle, string chatSubtitle, string activeAgentId, bool humanMode, CancellationToken cancellationToken = default);
    Task PublishSplitModeAsync(bool on, CancellationToken cancellationToken = default);
    Task PublishKnowledgeBaseAsync(IReadOnlyList<KbEntry> items, CancellationToken cancellationToken = default);
}
