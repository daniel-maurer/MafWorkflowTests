using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.AI;
using SalesWorkflow.Models;

namespace SalesWorkflow;

internal sealed class BffWorkflowClient : IAsyncDisposable
{
    private readonly WorkflowConfiguration _configuration;
    private readonly IChatClient _chatClient;
    private readonly Func<IUserInteractor, Workflow> _workflowFactory;
    private readonly HubConnection _connection;
    private readonly ConcurrentDictionary<string, WorkflowSession> _sessions = new();
    private readonly ConcurrentDictionary<string, (string Text, DateTime Timestamp)> _lastUserMessages = new();

    public BffWorkflowClient(
        WorkflowConfiguration configuration,
        IChatClient chatClient,
        Func<IUserInteractor, Workflow> workflowFactory)
    {
        _configuration = configuration;
        _chatClient = chatClient;
        _workflowFactory = workflowFactory;

        _connection = new HubConnectionBuilder()
            .WithUrl(_configuration.BffBaseUrl, options =>
            {
                options.Headers.Add("Authorization", "Bearer mock-token:maf-sales-worker");
            })
            .WithAutomaticReconnect()
            .Build();

        RegisterHubHandlers();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _connection.StartAsync(cancellationToken);
        await _connection.InvokeAsync(
            "RegisterWorker",
            _configuration.WorkerId,
            new[] { "sales-assistant" },
            cancellationToken);

        await PublishTraceAsync(string.Empty, "MAF Sales Worker connected to BFF.");
    }

    private void RegisterHubHandlers()
    {
        _connection.On<MafStartWorkflowCommand>("startWorkflow", async command =>
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

        _connection.On<MafUserMessageCommand>("userMessage", async command =>
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

        _connection.On<MafHumanMessageCommand>("humanMessage", async command =>
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

        _connection.On<MafRunScenarioCommand>("runScenario", async command =>
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

        _connection.On<MafSessionCommand>("markSolved", async command =>
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

        _connection.On<MafSessionCommand>("resetWorkflow", async command =>
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

    private async Task HandleStartWorkflowAsync(MafStartWorkflowCommand command)
    {
        var session = _sessions.GetOrAdd(command.SessionId, id => CreateSession(id, command.WorkflowId));
        await session.StartAsync(command.InitialMessage ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(command.InitialMessage))
        {
            await PublishMessageAsync(command.SessionId, CreateUserMessage(command.InitialMessage));
        }

        await PublishTraceAsync(command.SessionId, "Fluxo comercial MAF iniciado.");
        await PublishAgentStateAsync(command.SessionId, "intent", "active", "Running");
        await PublishContextAsync(command.SessionId, new MafContextPayload
        {
            Status = "analyzing-intent",
            ChatTitle = "Sales Assistant",
            ChatSubtitle = "Identificando intenção e catálogo...",
            ActiveAgentId = "intent",
            HumanMode = false
        });
    }

    private async Task HandleUserMessageAsync(MafUserMessageCommand command)
    {
        if (!_sessions.TryGetValue(command.SessionId, out var session))
        {
            // Esta sessão pertence a outro workflow/worker em execução
            return;
        }

        var now = DateTime.UtcNow;
        if (_lastUserMessages.TryGetValue(command.SessionId, out var last)
            && last.Text == command.Text
            && (now - last.Timestamp).TotalMilliseconds < 1500)
        {
            Logger.LogWarning($"[Deduplication] Mensagem duplicada ignorada para sessão {command.SessionId}: '{command.Text}'");
            return;
        }
        _lastUserMessages[command.SessionId] = (command.Text, now);

        await PublishMessageAsync(command.SessionId, CreateUserMessage(command.Text));
        await PublishTraceAsync(command.SessionId, "Mensagem do cliente recebida.");
        await session.EnqueueMessageAsync(command.Text);
    }

    private async Task HandleHumanMessageAsync(MafHumanMessageCommand command)
    {
        if (!_sessions.TryGetValue(command.SessionId, out var session))
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
            AgentId = "human-seller",
            SystemStyle = null,
            Text = command.Text,
            Tools = Array.Empty<object>(),
            CreatedAt = DateTime.UtcNow,
            SplitMirror = true,
            Audience = MessageAudience.Both,
        });

        await session.EnqueueMessageAsync(command.Text);
        await PublishTraceAsync(command.SessionId, "Mensagem do vendedor roteada para a sessão.");
    }

    private async Task HandleRunScenarioAsync(MafRunScenarioCommand command)
    {
        if (!_sessions.TryGetValue(command.SessionId, out var session))
        {
            Logger.LogWarning($"Received runScenario for unknown session {command.SessionId}.");
            return;
        }

        await PublishTraceAsync(command.SessionId, $"Cenário '{command.ScenarioId}' disparado.");
        await session.EnqueueMessageAsync(command.ScenarioId);
    }

    private async Task HandleMarkSolvedAsync(MafSessionCommand command)
    {
        if (_sessions.TryGetValue(command.SessionId, out var session))
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

    private async Task HandleResetWorkflowAsync(MafSessionCommand command)
    {
        if (_sessions.TryRemove(command.SessionId, out var session))
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

    private async Task PublishMessageAsync(string sessionId, MafMessagePayload payload)
    {
        await PublishPublicEventAsync(sessionId, "message", payload);
    }

    private async Task PublishTraceAsync(string sessionId, string title, string level = "info")
    {
        await PublishPublicEventAsync(sessionId, "trace", new MafTracePayload
        {
            Id = GenerateId("trc"),
            Time = DateTime.UtcNow,
            Title = title,
            Level = level
        });
    }

    private async Task PublishAgentAsync(string sessionId, MafAgentPayload payload)
    {
        await PublishPublicEventAsync(sessionId, "agent", payload);
    }

    private async Task PublishKbAsync(string sessionId, IEnumerable<MafKbPayload> payload)
    {
        await PublishPublicEventAsync(sessionId, "kb", payload);
    }

    private async Task PublishTypingAsync(string sessionId, bool on, string label = "Consultor digitando...", string container = "msgs")
    {
        await PublishPublicEventAsync(sessionId, "typing", new MafTypingPayload
        {
            Container = container,
            Label = label,
            On = on
        });
    }

    private async Task PublishContextAsync(string sessionId, MafContextPayload payload)
    {
        await PublishPublicEventAsync(sessionId, "context", payload);
    }

    private async Task PublishAgentStateAsync(string sessionId, string agentId, string state, string tag)
    {
        await PublishAgentAsync(sessionId, new MafAgentPayload
        {
            Id = agentId,
            State = state,
            Tag = tag,
            ActiveTools = Array.Empty<object>()
        });
    }

    private async Task PublishPublicEventAsync(string sessionId, string eventType, object payload)
    {
        var envelope = new MafEventEnvelope
        {
            SessionId = sessionId,
            EventType = eventType,
            Payload = payload,
            OccurredAt = DateTime.UtcNow,
            SequenceId = Guid.NewGuid().ToString("N")
        };

        await _connection.InvokeAsync("PublishEvent", envelope);
    }

    private static string GenerateId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    private MafMessagePayload CreateUserMessage(string text)
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

    private MafMessagePayload CreateAgentMessage(
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

    private MafMessagePayload CreateSystemMessage(string text, string systemStyle, string audience = MessageAudience.Both)
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

    private WorkflowSession CreateSession(string sessionId, string workflowId = "sales-assistant")
    {
        var interactor = new SessionWorkflowInteractor(sessionId, this);
        Workflow workflow = workflowId switch
        {
            "sales-assistant" => WorkflowFactory.BuildSalesWorkflow(_chatClient, interactor),
            _ => _workflowFactory(interactor)
        };
        return new WorkflowSession(sessionId, workflow, this, interactor);
    }

    private sealed class WorkflowSession : IAsyncDisposable
    {
        private readonly string _sessionId;
        private readonly Workflow _workflow;
        private readonly BffWorkflowClient _parent;
        private readonly SessionWorkflowInteractor _userInteractor;
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Task _runnerTask;

        public WorkflowSession(string sessionId, Workflow workflow, BffWorkflowClient parent, SessionWorkflowInteractor userInteractor)
        {
            _sessionId = sessionId;
            _workflow = workflow;
            _parent = parent;
            _userInteractor = userInteractor;
            _runnerTask = Task.Run(RunAsync);
        }

        public async Task StartAsync(string initialMessage)
        {
            if (!string.IsNullOrWhiteSpace(initialMessage))
            {
                await EnqueueMessageAsync(initialMessage);
            }

            _started.TrySetResult();
        }

        public async Task EnqueueMessageAsync(string message)
        {
            await _userInteractor.EnqueueMessageAsync(message);
        }

        private async Task RunAsync()
        {
            await _started.Task;

            try
            {
                await using var handle = await InProcessExecution.StreamAsync(_workflow, string.Empty);
                await foreach (var evt in handle.WatchStreamAsync())
                {
                    switch (evt)
                    {
                        case RequestInfoEvent requestInfoEvent:
                            var incomingMessage = await _userInteractor.ReadNextMessageAsync();
                            var response = requestInfoEvent.Request.CreateResponse(incomingMessage);
                            await handle.SendResponseAsync(response);
                            break;
                        case WorkflowOutputEvent workflowOutput:
                            await _parent.PublishWorkflowOutputAsync(_sessionId, workflowOutput.Data);
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Session {_sessionId} failed: {ex.Message}");
                await _parent.PublishTraceAsync(_sessionId, $"Session error: {ex.Message}", "error");
            }
        }

        public async ValueTask DisposeAsync()
        {
            _userInteractor.Complete();
            await _runnerTask;
        }
    }

    private sealed class SessionWorkflowInteractor : IUserInteractor
    {
        private readonly string _sessionId;
        private readonly BffWorkflowClient _parent;
        private readonly Channel<string> _incomingMessages = Channel.CreateUnbounded<string>();

        public SessionWorkflowInteractor(string sessionId, BffWorkflowClient parent)
        {
            _sessionId = sessionId;
            _parent = parent;
        }

        public async Task SendUserResponseAsync(
            string prompt,
            string? agentId = null,
            IReadOnlyList<AgentToolCall>? tools = null,
            string audience = MessageAudience.Both,
            IReadOnlyList<MafImagePayload>? images = null,
            CancellationToken cancellationToken = default)
        {
            await _parent.PublishMessageAsync(_sessionId, _parent.CreateAgentMessage(prompt, agentId, tools, audience, images));
        }

        public async Task SendSystemMessageAsync(string text, string systemStyle = "handoff", string audience = MessageAudience.Both, CancellationToken cancellationToken = default)
        {
            await _parent.PublishMessageAsync(_sessionId, _parent.CreateSystemMessage(text, systemStyle, audience));
        }

        public async Task<string> GetUserResponseAsync(
            string prompt,
            string? agentId = null,
            IReadOnlyList<AgentToolCall>? tools = null,
            string audience = MessageAudience.Both,
            IReadOnlyList<MafImagePayload>? images = null,
            CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrWhiteSpace(prompt))
            {
                await _parent.PublishMessageAsync(_sessionId, _parent.CreateAgentMessage(prompt, agentId, tools, audience, images));
            }

            return await _incomingMessages.Reader.ReadAsync(cancellationToken);
        }

        public async Task SetAgentTypingAsync(string label, bool on, CancellationToken cancellationToken = default)
        {
            await _parent.PublishTypingAsync(_sessionId, on, label);
        }

        public async Task PublishTraceAsync(string title, string level = "info", CancellationToken cancellationToken = default)
        {
            await _parent.PublishTraceAsync(_sessionId, title, level);
        }

        public async Task PublishAgentStateAsync(string agentId, string state, string tag, CancellationToken cancellationToken = default)
        {
            await _parent.PublishAgentStateAsync(_sessionId, agentId, state, tag);
        }

        public async Task PublishContextAsync(string status, string chatTitle, string chatSubtitle, string activeAgentId, bool humanMode, CancellationToken cancellationToken = default)
        {
            await _parent.PublishContextAsync(_sessionId, new MafContextPayload
            {
                Status = status,
                ChatTitle = chatTitle,
                ChatSubtitle = chatSubtitle,
                ActiveAgentId = activeAgentId,
                HumanMode = humanMode
            });
        }

        public async Task PublishSplitModeAsync(bool on, CancellationToken cancellationToken = default)
        {
            await _parent.PublishPublicEventAsync(_sessionId, "splitMode", on);
        }

        public async Task PublishKnowledgeBaseAsync(IReadOnlyList<KbEntry> items, CancellationToken cancellationToken = default)
        {
            var payload = items.Select(item => new MafKbPayload
            {
                Id = string.IsNullOrWhiteSpace(item.Id) ? GenerateId("kb") : item.Id,
                Title = item.Title,
                Category = item.Category,
                Score = item.Score,
                Summary = item.Summary,
                ResolutionType = item.ResolutionType,
                Tags = item.Tags?.ToArray() ?? Array.Empty<string>(),
            }).ToArray();
            await _parent.PublishKbAsync(_sessionId, payload);
        }

        public async Task<string> ReadNextMessageAsync(CancellationToken cancellationToken = default)
        {
            return await _incomingMessages.Reader.ReadAsync(cancellationToken);
        }

        public async Task EnqueueMessageAsync(string message)
        {
            await _incomingMessages.Writer.WriteAsync(message);
        }

        public void Complete()
        {
            _incomingMessages.Writer.Complete();
        }
    }

    private async Task PublishWorkflowOutputAsync(string sessionId, object? outputData)
    {
        switch (outputData)
        {
            case IntentResult intentResult:
                await PublishTraceAsync(
                    sessionId,
                    $"Triagem: {intentResult.Intent} | Sentimento: {intentResult.CustomerSentiment}",
                    "info");
                break;

            case CatalogResult catalogResult:
                var summary = catalogResult.HasResults
                    ? $"{catalogResult.Products.Count} produto(s) localizado(s) no estoque."
                    : "Nenhum produto correspondente.";
                await PublishTraceAsync(sessionId, $"Catálogo: {summary}", catalogResult.HasResults ? "success" : "warning");
                break;

            case SalesAdviceResult adviceResult:
                await PublishTraceAsync(
                    sessionId,
                    $"Consultoria de Vendas: {adviceResult.SuggestedComplements.Count} complementos, {adviceResult.SuggestedKits.Count} kits.",
                    "info");
                break;

            case QuoteResult quoteResult:
                await PublishTraceAsync(
                    sessionId,
                    $"Orçamento {quoteResult.QuoteId} consolidado: Total R$ {quoteResult.Total:N2}.",
                    "success");
                break;

            case FollowUpResult followUpResult:
                await PublishTraceAsync(
                    sessionId,
                    $"Follow-up agendado: {followUpResult.FollowUpType} via {followUpResult.Channel}.",
                    "info");
                break;

            case SalesResolutionResult resolutionResult:
                await PublishTraceAsync(
                    sessionId,
                    $"Atendimento comercial: {resolutionResult.ResolutionType} (Resolvido={resolutionResult.IsResolved}).",
                    resolutionResult.IsResolved ? "success" : "info");
                break;

            case SalesRecord salesRecord:
                await PublishTraceAsync(
                    sessionId,
                    $"Sessão comercial finalizada: {salesRecord.Outcome} | Sentimento={salesRecord.CustomerSentiment}.",
                    "success");
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var session in _sessions.Values)
        {
            await session.DisposeAsync();
        }
        _sessions.Clear();
        await _connection.DisposeAsync();
    }
}

// === Payload DTOs ===
public sealed class MafEventEnvelope
{
    [JsonPropertyName("sessionId")] public string SessionId { get; set; } = string.Empty;
    [JsonPropertyName("eventType")] public string EventType { get; set; } = string.Empty;
    [JsonPropertyName("payload")] public object? Payload { get; set; }
    [JsonPropertyName("occurredAt")] public DateTime OccurredAt { get; set; }
    [JsonPropertyName("sequenceId")] public string SequenceId { get; set; } = string.Empty;
}

public sealed class MafImagePayload
{
    [JsonPropertyName("url")] public string Url { get; set; } = string.Empty;
    [JsonPropertyName("alt")] public string Alt { get; set; } = string.Empty;
    [JsonPropertyName("sku")] public string? Sku { get; set; }
}

public sealed class MafMessagePayload
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("side")] public string Side { get; set; } = string.Empty;
    [JsonPropertyName("senderType")] public string SenderType { get; set; } = string.Empty;
    [JsonPropertyName("agentId")] public string? AgentId { get; set; }
    [JsonPropertyName("systemStyle")] public string? SystemStyle { get; set; }
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
    [JsonPropertyName("tools")] public IReadOnlyList<object> Tools { get; set; } = Array.Empty<object>();
    [JsonPropertyName("createdAt")] public DateTime CreatedAt { get; set; }
    [JsonPropertyName("splitMirror")] public bool SplitMirror { get; set; }
    [JsonPropertyName("audience")] public string Audience { get; set; } = MessageAudience.Both;
    [JsonPropertyName("images")] public IReadOnlyList<MafImagePayload>? Images { get; set; }
}

public sealed class MafToolCallPayload
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("args")] public string Args { get; set; } = string.Empty;
    [JsonPropertyName("ok")] public bool Ok { get; set; } = true;
}

public sealed class MafTracePayload
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("time")] public DateTime Time { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("level")] public string Level { get; set; } = "info";
}

public sealed class MafTypingPayload
{
    [JsonPropertyName("container")] public string Container { get; set; } = "msgs";
    [JsonPropertyName("label")] public string Label { get; set; } = string.Empty;
    [JsonPropertyName("on")] public bool On { get; set; }
}

public sealed class MafAgentPayload
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("state")] public string State { get; set; } = string.Empty;
    [JsonPropertyName("tag")] public string Tag { get; set; } = string.Empty;
    [JsonPropertyName("activeTools")] public IReadOnlyList<object> ActiveTools { get; set; } = Array.Empty<object>();
}

public sealed class MafContextPayload
{
    [JsonPropertyName("status")] public string Status { get; set; } = string.Empty;
    [JsonPropertyName("chatTitle")] public string ChatTitle { get; set; } = string.Empty;
    [JsonPropertyName("chatSubtitle")] public string ChatSubtitle { get; set; } = string.Empty;
    [JsonPropertyName("activeAgentId")] public string ActiveAgentId { get; set; } = string.Empty;
    [JsonPropertyName("humanMode")] public bool HumanMode { get; set; }
}

public sealed class MafKbPayload
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("category")] public string Category { get; set; } = string.Empty;
    [JsonPropertyName("score")] public double Score { get; set; }
    [JsonPropertyName("summary")] public string Summary { get; set; } = string.Empty;
    [JsonPropertyName("resolutionType")] public string ResolutionType { get; set; } = string.Empty;
    [JsonPropertyName("tags")] public IReadOnlyList<string> Tags { get; set; } = Array.Empty<string>();
}

public sealed record MafStartWorkflowCommand(
    string SessionId,
    string WorkflowId,
    string TicketId,
    string? InitialMessage,
    string WorkflowName,
    string Version,
    string InputSchema);

public sealed record MafUserMessageCommand(string SessionId, string Text);
public sealed record MafHumanMessageCommand(string SessionId, string Text);
public sealed record MafRunScenarioCommand(string SessionId, string ScenarioId);
public sealed record MafSessionCommand(string SessionId);
