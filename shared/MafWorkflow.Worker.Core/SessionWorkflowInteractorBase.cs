using System.Threading.Channels;
using MafWorkflow.Shared;

namespace MafWorkflow.Worker.Core;

public class SessionWorkflowInteractorBase : IUserInteractor
{
    protected readonly string SessionId;
    protected readonly BffWorkflowClientBase Parent;
    protected readonly Channel<string> IncomingMessages = Channel.CreateUnbounded<string>();

    public SessionWorkflowInteractorBase(string sessionId, BffWorkflowClientBase parent)
    {
        SessionId = sessionId;
        Parent = parent;
    }

    public virtual async Task SendUserResponseAsync(
        string prompt,
        string? agentId = null,
        IReadOnlyList<AgentToolCall>? tools = null,
        string audience = MessageAudience.Both,
        IReadOnlyList<MafImagePayload>? images = null,
        CancellationToken cancellationToken = default)
    {
        await Parent.PublishMessageAsync(SessionId, Parent.CreateAgentMessage(prompt, agentId, tools, audience, images));
    }

    public virtual async Task SendSystemMessageAsync(
        string text,
        string systemStyle = "handoff",
        string audience = MessageAudience.Both,
        CancellationToken cancellationToken = default)
    {
        await Parent.PublishMessageAsync(SessionId, Parent.CreateSystemMessage(text, systemStyle, audience));
    }

    public virtual async Task<string> GetUserResponseAsync(
        string prompt,
        string? agentId = null,
        IReadOnlyList<AgentToolCall>? tools = null,
        string audience = MessageAudience.Both,
        IReadOnlyList<MafImagePayload>? images = null,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(prompt))
        {
            await Parent.PublishMessageAsync(SessionId, Parent.CreateAgentMessage(prompt, agentId, tools, audience, images));
        }

        return await IncomingMessages.Reader.ReadAsync(cancellationToken);
    }

    public virtual async Task SetAgentTypingAsync(string label, bool on, CancellationToken cancellationToken = default)
    {
        await Parent.PublishTypingAsync(SessionId, on, label);
    }

    public virtual async Task PublishTraceAsync(string title, string level = "info", CancellationToken cancellationToken = default)
    {
        await Parent.PublishTraceAsync(SessionId, title, level);
    }

    public virtual async Task PublishAgentStateAsync(string agentId, string state, string tag, CancellationToken cancellationToken = default)
    {
        await Parent.PublishAgentStateAsync(SessionId, agentId, state, tag);
    }

    public virtual async Task PublishContextAsync(string status, string chatTitle, string chatSubtitle, string activeAgentId, bool humanMode, CancellationToken cancellationToken = default)
    {
        await Parent.PublishContextAsync(SessionId, new MafContextPayload
        {
            Status = status,
            ChatTitle = chatTitle,
            ChatSubtitle = chatSubtitle,
            ActiveAgentId = activeAgentId,
            HumanMode = humanMode
        });
    }

    public virtual async Task PublishSplitModeAsync(bool on, CancellationToken cancellationToken = default)
    {
        await Parent.PublishPublicEventAsync(SessionId, "splitMode", on);
    }

    public virtual async Task PublishKnowledgeBaseAsync(IReadOnlyList<KbEntry> items, CancellationToken cancellationToken = default)
    {
        var payload = items.Select(item => new MafKbPayload
        {
            Id = string.IsNullOrWhiteSpace(item.Id) ? BffWorkflowClientBase.GenerateId("kb") : item.Id,
            Title = item.Title,
            Category = item.Category,
            Score = item.Score,
            Summary = item.Summary,
            ResolutionType = item.ResolutionType,
            Tags = item.Tags?.ToArray() ?? Array.Empty<string>(),
        }).ToArray();
        await Parent.PublishKbAsync(SessionId, payload);
    }

    public virtual async Task<string> ReadNextMessageAsync(CancellationToken cancellationToken = default)
    {
        return await IncomingMessages.Reader.ReadAsync(cancellationToken);
    }

    public virtual async Task EnqueueMessageAsync(string message)
    {
        await IncomingMessages.Writer.WriteAsync(message);
    }

    public virtual void Complete()
    {
        IncomingMessages.Writer.Complete();
    }
}
