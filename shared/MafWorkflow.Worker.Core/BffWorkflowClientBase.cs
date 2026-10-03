using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.AI;
using MafWorkflow.Shared;

namespace MafWorkflow.Worker.Core;

public abstract class BffWorkflowClientBase : IAsyncDisposable
{
    protected readonly WorkflowConfigurationBase Configuration;
    protected readonly IChatClient ChatClient;
    protected readonly HubConnection Connection;
    protected readonly ConcurrentDictionary<string, WorkflowSessionBase> Sessions = new();
    protected readonly ConcurrentDictionary<string, (string Text, DateTime Timestamp)> LastUserMessages = new();

    public abstract string[] SupportedWorkflowIds { get; }
    public virtual string WorkerRoleName => "MAF Worker";

    protected BffWorkflowClientBase(WorkflowConfigurationBase configuration, IChatClient chatClient)
    {
        Configuration = configuration;
        ChatClient = chatClient;

        Connection = new HubConnectionBuilder()
            .WithUrl(Configuration.BffBaseUrl, options =>
            {
                options.Headers.Add("Authorization", $"Bearer mock-token:{Configuration.WorkerId}");
            })
            .WithAutomaticReconnect()
            .Build();

        RegisterHubHandlers();
    }

    public virtual async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await Connection.StartAsync(cancellationToken);
        await Connection.InvokeAsync(
            "RegisterWorker",
            Configuration.WorkerId,
            SupportedWorkflowIds,
            cancellationToken);

        await PublishTraceAsync(string.Empty, $"{WorkerRoleName} connected to BFF.");
    }

    protected virtual void RegisterHubHandlers()
    {
        Connection.On<MafStartWorkflowCommand>("startWorkflow", async command =>
        {
            try
            {
                await HandleStartWorkflowAsync(command);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to handle startWorkflow: {ex.Message}");
            }
        });

        Connection.On<MafUserMessageCommand>("userMessage", async command =>
        {
            try
            {
                await HandleUserMessageAsync(command);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to handle userMessage: {ex.Message}");
            }
        });

        Connection.On<MafHumanMessageCommand>("humanMessage", async command =>
        {
            try
            {
                await HandleHumanMessageAsync(command);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to handle humanMessage: {ex.Message}");
            }
        });

        Connection.On<MafRunScenarioCommand>("runScenario", async command =>
        {
            try
            {
                await HandleRunScenarioAsync(command);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to handle runScenario: {ex.Message}");
            }
        });

        Connection.On<MafSessionCommand>("markSolved", async command =>
        {
            try
            {
                await HandleMarkSolvedAsync(command);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to handle markSolved: {ex.Message}");
            }
        });

        Connection.On<MafSessionCommand>("resetWorkflow", async command =>
        {
            try
            {
                await HandleResetWorkflowAsync(command);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to handle resetWorkflow: {ex.Message}");
            }
        });
    }

    protected abstract WorkflowSessionBase CreateSession(string sessionId, string workflowId);

    public abstract Task OnWorkflowOutputAsync(string sessionId, object? outputData);

    protected virtual async Task HandleStartWorkflowAsync(MafStartWorkflowCommand command)
    {
        var session = Sessions.GetOrAdd(command.SessionId, id => CreateSession(id, command.WorkflowId));
        await session.StartAsync(command.InitialMessage ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(command.InitialMessage))
        {
            await PublishMessageAsync(command.SessionId, CreateUserMessage(command.InitialMessage));
        }

        await OnWorkflowStartedAsync(command.SessionId, command);
    }

    protected virtual async Task OnWorkflowStartedAsync(string sessionId, MafStartWorkflowCommand command)
    {
        await PublishTraceAsync(sessionId, $"Fluxo {command.WorkflowId} iniciado.");
    }

    protected virtual async Task HandleUserMessageAsync(MafUserMessageCommand command)
    {
        if (!Sessions.TryGetValue(command.SessionId, out var session))
        {
            Logger.LogInfo($"[SessionRecovery] Sessão {command.SessionId} não encontrada em memória. Inicializando sob demanda...");
            var defaultWorkflowId = SupportedWorkflowIds.FirstOrDefault() ?? "default";
            session = Sessions.GetOrAdd(command.SessionId, id => CreateSession(id, defaultWorkflowId));
            await session.StartAsync(command.Text);

            await PublishMessageAsync(command.SessionId, CreateUserMessage(command.Text));
            await PublishTraceAsync(command.SessionId, "Fluxo iniciado sob demanda.");
            return;
        }

        var now = DateTime.UtcNow;
        if (LastUserMessages.TryGetValue(command.SessionId, out var last)
            && last.Text == command.Text
            && (now - last.Timestamp).TotalMilliseconds < 1500)
        {
            Logger.LogWarning($"[Deduplication] Mensagem duplicada ignorada para sessão {command.SessionId}: '{command.Text}'");
            return;
        }
        LastUserMessages[command.SessionId] = (command.Text, now);

        await PublishMessageAsync(command.SessionId, CreateUserMessage(command.Text));
        await PublishTraceAsync(command.SessionId, "Mensagem do cliente recebida.");
        await session.EnqueueMessageAsync(command.Text);
    }

    protected virtual async Task HandleHumanMessageAsync(MafHumanMessageCommand command)
    {
        if (!Sessions.TryGetValue(command.SessionId, out var session))
        {
            Logger.LogWarning($"Received humanMessage for unknown session {command.SessionId}.");
            return;
        }

        await PublishMessageAsync(command.SessionId, new MafMessagePayload
        {
            Id = GenerateId("msg"),
            Type = "message",
            Side = "left",
            SenderType = "human",
            AgentId = "human-attendant",
            SystemStyle = null,
            Text = command.Text,
            Tools = Array.Empty<object>(),
            CreatedAt = DateTime.UtcNow,
            SplitMirror = true,
            Audience = MessageAudience.Both,
        });

        await session.EnqueueMessageAsync(command.Text);
        await PublishTraceAsync(command.SessionId, "Mensagem do atendente humano roteada para a sessão.");
    }

    protected virtual async Task HandleRunScenarioAsync(MafRunScenarioCommand command)
    {
        if (!Sessions.TryGetValue(command.SessionId, out var session))
        {
            Logger.LogInfo($"[SessionRecovery] Sessão {command.SessionId} recuperada para runScenario.");
            var defaultWorkflowId = SupportedWorkflowIds.FirstOrDefault() ?? "default";
            session = Sessions.GetOrAdd(command.SessionId, id => CreateSession(id, defaultWorkflowId));
            await session.StartAsync(command.ScenarioId);
            return;
        }

        await PublishTraceAsync(command.SessionId, $"Cenário '{command.ScenarioId}' disparado.");
        await session.EnqueueMessageAsync(command.ScenarioId);
    }

    protected virtual async Task HandleMarkSolvedAsync(MafSessionCommand command)
    {
        if (Sessions.TryGetValue(command.SessionId, out var session))
        {
            await session.EnqueueMessageAsync(WorkflowControlTokens.MarkResolved);
            await PublishTraceAsync(command.SessionId, "Atendimento marcado como concluído.", "success");
        }
        else
        {
            await PublishPublicEventAsync(command.SessionId, "splitMode", false);
            await PublishContextAsync(command.SessionId, new MafContextPayload
            {
                Status = "resolved",
                ChatTitle = "Concluído",
                ChatSubtitle = "Atendimento finalizado.",
                ActiveAgentId = string.Empty,
                HumanMode = false
            });
        }
    }

    protected virtual async Task HandleResetWorkflowAsync(MafSessionCommand command)
    {
        if (Sessions.TryRemove(command.SessionId, out var session))
        {
            await session.DisposeAsync();
        }

        await PublishContextAsync(command.SessionId, new MafContextPayload
        {
            Status = "idle",
            ChatTitle = "Workflow reiniciado",
            ChatSubtitle = "Sessão reinicializada.",
            ActiveAgentId = string.Empty,
            HumanMode = false
        });
    }

    public virtual async Task PublishMessageAsync(string sessionId, MafMessagePayload payload)
    {
        await PublishPublicEventAsync(sessionId, "message", payload);
    }

    public virtual async Task PublishTraceAsync(string sessionId, string title, string level = "info")
    {
        await PublishPublicEventAsync(sessionId, "trace", new MafTracePayload
        {
            Id = GenerateId("trc"),
            Time = DateTime.UtcNow,
            Title = title,
            Level = level
        });
    }

    public virtual async Task PublishAgentAsync(string sessionId, MafAgentPayload payload)
    {
        await PublishPublicEventAsync(sessionId, "agent", payload);
    }

    public virtual async Task PublishKbAsync(string sessionId, IEnumerable<MafKbPayload> payload)
    {
        await PublishPublicEventAsync(sessionId, "kb", payload);
    }

    public virtual async Task PublishTypingAsync(string sessionId, bool on, string label = "Digitando...", string container = "msgs")
    {
        await PublishPublicEventAsync(sessionId, "typing", new MafTypingPayload
        {
            Container = container,
            Label = label,
            On = on
        });
    }

    public virtual async Task PublishContextAsync(string sessionId, MafContextPayload payload)
    {
        await PublishPublicEventAsync(sessionId, "context", payload);
    }

    public virtual async Task PublishAgentStateAsync(string sessionId, string agentId, string state, string tag)
    {
        await PublishAgentAsync(sessionId, new MafAgentPayload
        {
            Id = agentId,
            State = state,
            Tag = tag,
            ActiveTools = Array.Empty<object>()
        });
    }

    public virtual async Task PublishPublicEventAsync(string sessionId, string eventType, object payload)
    {
        var envelope = new MafEventEnvelope
        {
            SessionId = sessionId,
            EventType = eventType,
            Payload = payload,
            OccurredAt = DateTime.UtcNow,
            SequenceId = Guid.NewGuid().ToString("N")
        };

        await Connection.InvokeAsync("PublishEvent", envelope);
    }

    public static string GenerateId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    public virtual MafMessagePayload CreateUserMessage(string text)
    {
        return new MafMessagePayload
        {
            Id = GenerateId("msg"),
            Type = "message",
            Side = "right",
            SenderType = "user",
            AgentId = null,
            SystemStyle = null,
            Text = text,
            Tools = Array.Empty<object>(),
            CreatedAt = DateTime.UtcNow,
            SplitMirror = true,
        };
    }

    public virtual MafMessagePayload CreateAgentMessage(
        string text,
        string? agentId,
        IReadOnlyList<AgentToolCall>? tools,
        string audience = MessageAudience.Both,
        IReadOnlyList<MafImagePayload>? images = null)
    {
        return new MafMessagePayload
        {
            Id = GenerateId("msg"),
            Type = "message",
            Side = "left",
            SenderType = "agent",
            AgentId = agentId,
            SystemStyle = null,
            Text = text,
            Tools = tools?.Select(tool => new MafToolCallPayload
            {
                Name = tool.Name,
                Args = tool.Args,
                Ok = tool.Ok
            }).ToArray() ?? Array.Empty<MafToolCallPayload>(),
            CreatedAt = DateTime.UtcNow,
            SplitMirror = false,
            Audience = audience,
            Images = images,
        };
    }

    public virtual MafMessagePayload CreateSystemMessage(string text, string systemStyle, string audience = MessageAudience.Both)
    {
        return new MafMessagePayload
        {
            Id = GenerateId("msg"),
            Type = "system",
            Side = "left",
            SenderType = "system",
            AgentId = null,
            SystemStyle = systemStyle,
            Text = text,
            Tools = Array.Empty<object>(),
            CreatedAt = DateTime.UtcNow,
            SplitMirror = false,
            Audience = audience,
        };
    }

    public virtual async ValueTask DisposeAsync()
    {
        foreach (var session in Sessions.Values)
        {
            await session.DisposeAsync();
        }
        Sessions.Clear();
        await Connection.DisposeAsync();
    }
}
