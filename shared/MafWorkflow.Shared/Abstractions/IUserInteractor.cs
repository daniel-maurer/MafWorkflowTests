namespace MafWorkflow.Shared;

public interface IUserInteractor
{
    Task<string> GetUserResponseAsync(
        string prompt,
        string? agentId = null,
        IReadOnlyList<AgentToolCall>? tools = null,
        string audience = MessageAudience.Both,
        IReadOnlyList<MafImagePayload>? images = null,
        CancellationToken cancellationToken = default);

    Task SendUserResponseAsync(
        string prompt,
        string? agentId = null,
        IReadOnlyList<AgentToolCall>? tools = null,
        string audience = MessageAudience.Both,
        IReadOnlyList<MafImagePayload>? images = null,
        CancellationToken cancellationToken = default);

    Task SendSystemMessageAsync(
        string text,
        string systemStyle = "handoff",
        string audience = MessageAudience.Both,
        CancellationToken cancellationToken = default);

    Task SetAgentTypingAsync(
        string label,
        bool on,
        CancellationToken cancellationToken = default);

    Task PublishTraceAsync(
        string title,
        string level = "info",
        CancellationToken cancellationToken = default);

    Task PublishAgentStateAsync(
        string agentId,
        string state,
        string tag,
        CancellationToken cancellationToken = default);

    Task PublishContextAsync(
        string status,
        string chatTitle,
        string chatSubtitle,
        string activeAgentId,
        bool humanMode,
        CancellationToken cancellationToken = default);

    Task PublishSplitModeAsync(
        bool on,
        CancellationToken cancellationToken = default);

    Task PublishKnowledgeBaseAsync(
        IReadOnlyList<KbEntry> items,
        CancellationToken cancellationToken = default);
}
