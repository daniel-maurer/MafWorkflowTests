namespace SalesWorkflow;

public sealed class AgentToolCall
{
    public string Name { get; init; } = string.Empty;
    public string Args { get; init; } = string.Empty;
    public bool Ok { get; init; } = true;
}

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

public static class MessageAudience
{
    public const string Both = "both";
    public const string Client = "client";
    public const string Attendant = "attendant";
    public const string Internal = "internal";
}

internal static class WorkflowControlTokens
{
    public const string MarkResolved = "__MAF_CONTROL__:mark-resolved";
    public const string Cancel = "__MAF_CONTROL__:cancel";
}

internal interface IUserInteractor
{
    Task<string> GetUserResponseAsync(string prompt, string? agentId = null, IReadOnlyList<AgentToolCall>? tools = null, string audience = MessageAudience.Both, CancellationToken cancellationToken = default);
    Task SendUserResponseAsync(string prompt, string? agentId = null, IReadOnlyList<AgentToolCall>? tools = null, string audience = MessageAudience.Both, IReadOnlyList<MafImagePayload>? images = null, CancellationToken cancellationToken = default);
    Task SendSystemMessageAsync(string text, string systemStyle = "handoff", string audience = MessageAudience.Both, CancellationToken cancellationToken = default);
    Task SetAgentTypingAsync(string label, bool on, CancellationToken cancellationToken = default);
    Task PublishTraceAsync(string title, string level = "info", CancellationToken cancellationToken = default);
    Task PublishAgentStateAsync(string agentId, string state, string tag, CancellationToken cancellationToken = default);
    Task PublishContextAsync(string status, string chatTitle, string chatSubtitle, string activeAgentId, bool humanMode, CancellationToken cancellationToken = default);
    Task PublishSplitModeAsync(bool on, CancellationToken cancellationToken = default);
    Task PublishKnowledgeBaseAsync(IReadOnlyList<KbEntry> items, CancellationToken cancellationToken = default);
}
