namespace SupportWorkflow;

/// <summary>
/// Identity of an agent for chat-message rendering on the frontend.
/// </summary>
public sealed class AgentIdentity
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = "MAF Agent";
    public string Icon { get; init; } = "git-branch";
    public string BubbleStyle { get; init; } = "agent";
    public string ColorTheme { get; init; } = "primary";

    public static readonly AgentIdentity Default = new()
    {
        Id = "maf",
        Name = "MAF Agent",
        Icon = "git-branch",
        BubbleStyle = "agent",
        ColorTheme = "primary",
    };
}

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
/// Handles user-facing interaction for the support workflow (chat, KB, traces, agent state, etc.).
/// </summary>
internal interface IUserInteractor
{
    Task<string> GetUserResponseAsync(string prompt, AgentIdentity? agent = null, IReadOnlyList<AgentToolCall>? tools = null, CancellationToken cancellationToken = default);
    Task SendUserResponseAsync(string prompt, AgentIdentity? agent = null, IReadOnlyList<AgentToolCall>? tools = null, CancellationToken cancellationToken = default);
    Task SetAgentTypingAsync(string label, bool on, CancellationToken cancellationToken = default);
    Task PublishTraceAsync(string title, string icon = "terminal", string color = "primary", CancellationToken cancellationToken = default);
    Task PublishAgentStateAsync(string agentId, string state, string tag, CancellationToken cancellationToken = default);
    Task PublishContextAsync(string status, string chatTitle, string chatSubtitle, string activeAgentId, bool humanMode, CancellationToken cancellationToken = default);
    Task PublishSplitModeAsync(bool on, CancellationToken cancellationToken = default);
    Task PublishKnowledgeBaseAsync(IReadOnlyList<KbEntry> items, CancellationToken cancellationToken = default);
}
