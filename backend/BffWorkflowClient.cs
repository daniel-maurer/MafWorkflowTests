using System.Collections.Concurrent;
using System.Threading.Channels;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.AI;

namespace SupportWorkflow;

internal sealed class BffWorkflowClient : IAsyncDisposable
{
    private readonly WorkflowConfiguration _configuration;
    private readonly IChatClient _chatClient;
    private readonly HubConnection _connection;
    private readonly ConcurrentDictionary<string, WorkflowSession> _sessions = new();
    private readonly Func<IUserInteractor, Workflow> _workflowFactory;

    // Central registry of agent identities used to render chat messages correctly on the frontend.
    // Keeping this in one place ensures every executor surfaces its own icon/name/color rather than
    // a generic "MAF Agent" badge.
    public static readonly IReadOnlyDictionary<string, AgentIdentity> AgentRegistry =
        new Dictionary<string, AgentIdentity>(StringComparer.OrdinalIgnoreCase)
        {
            ["triage"] = new() { Id = "triage", Name = "Triage Agent", Icon = "git-branch", BubbleStyle = "triage", ColorTheme = "primary" },
            ["freq"] = new() { Id = "freq", Name = "Freq. Problem Agent", Icon = "database", BubbleStyle = "freq", ColorTheme = "warning" },
            ["res"] = new() { Id = "res", Name = "Resolution Agent", Icon = "wrench", BubbleStyle = "res", ColorTheme = "success" },
            ["pattern"] = new() { Id = "pattern", Name = "Pattern Record Agent", Icon = "bar-chart-2", BubbleStyle = "pattern", ColorTheme = "error" },
            ["human-support"] = new() { Id = "human-support", Name = "Sarah M.", Icon = "headphones", BubbleStyle = "human", ColorTheme = "human" },
            ["maf"] = AgentIdentity.Default,
        };

    public BffWorkflowClient(WorkflowConfiguration configuration, IChatClient chatClient, Func<IUserInteractor, Workflow> workflowFactory)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
        _workflowFactory = workflowFactory ?? throw new ArgumentNullException(nameof(workflowFactory));
        _connection = new HubConnectionBuilder()
            .WithUrl(configuration.BffBaseUrl, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult("mock-token:maf-worker")!;
            })
            .WithAutomaticReconnect()
            .Build();

        ConfigureHandlers();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _connection.StartAsync(cancellationToken);
        await _connection.InvokeAsync("RegisterWorker", _configuration.WorkerId, new[] { "support", "incident-triage" }, cancellationToken);
        await PublishTraceAsync(string.Empty, "MAF worker connected to BFF.", TraceConstants.IconTerminal, TraceConstants.ColorPrimary);
    }

    private void ConfigureHandlers()
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
        var session = _sessions.GetOrAdd(command.SessionId, id => CreateSession(id));
        await session.StartAsync(command.InitialMessage ?? string.Empty);

        if (!string.IsNullOrWhiteSpace(command.InitialMessage))
        {
            await PublishMessageAsync(command.SessionId, CreateUserMessage(command.InitialMessage));
        }

        await PublishTraceAsync(command.SessionId, "Workflow started.", TraceConstants.IconGitBranch, TraceConstants.ColorPrimary);
        await PublishAgentStateAsync(command.SessionId, "triage", "active", "Running");
        await PublishContextAsync(command.SessionId, new MafContextPayload
        {
            Status = "triaging",
            ChatTitle = "MAF workflow started",
            ChatSubtitle = "Waiting for input and triage processing.",
            ActiveAgentId = "triage",
            HumanMode = false
        });
    }

    private async Task HandleUserMessageAsync(MafUserMessageCommand command)
    {
        var session = _sessions.GetOrAdd(command.SessionId, id => CreateSession(id));
        await PublishMessageAsync(command.SessionId, CreateUserMessage(command.Text));
        await PublishTraceAsync(command.SessionId, "User message received.", TraceConstants.IconGitBranch, TraceConstants.ColorPrimary);
        await session.EnqueueMessageAsync(command.Text);
    }

    private async Task HandleHumanMessageAsync(MafHumanMessageCommand command)
    {
        if (!_sessions.TryGetValue(command.SessionId, out var session))
        {
            Logger.LogWarning($"Received humanMessage for unknown session {command.SessionId}.");
            return;
        }

        var human = AgentRegistry["human-support"];
        await PublishMessageAsync(command.SessionId, new MafMessagePayload
        {
            Id = GenerateId("msg"),
            Type = "message",
            Side = "left",
            SenderType = "human",
            SenderName = human.Name,
            Icon = human.Icon,
            BubbleStyle = human.BubbleStyle,
            SystemStyle = null,
            Text = command.Text,
            Tools = Array.Empty<object>(),
            CreatedAt = DateTime.UtcNow,
            SplitMirror = true,
        });
        await session.EnqueueMessageAsync(command.Text);
        await PublishTraceAsync(command.SessionId, "Human message received and routed to session.", TraceConstants.IconUserCheck, TraceConstants.ColorPrimary);
    }

    private async Task HandleRunScenarioAsync(MafRunScenarioCommand command)
    {
        if (!_sessions.TryGetValue(command.SessionId, out var session))
        {
            Logger.LogWarning($"Received runScenario for unknown session {command.SessionId}.");
            return;
        }

        await PublishTraceAsync(command.SessionId, $"Scenario '{command.ScenarioId}' requested.", TraceConstants.IconTag, TraceConstants.ColorPrimary);
        await session.EnqueueMessageAsync(command.ScenarioId);
    }

    private async Task HandleMarkSolvedAsync(MafSessionCommand command)
    {
        if (_sessions.TryRemove(command.SessionId, out var session))
        {
            await session.DisposeAsync();
        }

        await PublishPublicEventAsync(command.SessionId, "splitMode", false);
        await PublishContextAsync(command.SessionId, new MafContextPayload
        {
            Status = "resolved",
            ChatTitle = "Resolved",
            ChatSubtitle = "The session was marked solved.",
            ActiveAgentId = string.Empty,
            HumanMode = false
        });
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
            ChatTitle = "Workflow reset",
            ChatSubtitle = "The session has been reset.",
            ActiveAgentId = string.Empty,
            HumanMode = false
        });
    }

    private async Task PublishMessageAsync(string sessionId, MafMessagePayload payload)
    {
        await PublishPublicEventAsync(sessionId, "message", payload);
    }

    private async Task PublishTraceAsync(string sessionId, string title, string icon = "git-branch", string color = "primary")
    {
        await PublishPublicEventAsync(sessionId, "trace", new MafTracePayload
        {
            Id = GenerateId("trc"),
            Time = DateTime.UtcNow,
            Icon = icon,
            Color = color,
            Title = title,
            Level = color
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

    private async Task PublishTypingAsync(string sessionId, bool on, string label = "MAF Agent typing", string container = "msgs")
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

    private static AgentIdentity ResolveAgent(AgentIdentity? agent) =>
        agent is null
            ? AgentIdentity.Default
            : AgentRegistry.TryGetValue(agent.Id, out var registered) ? registered : agent;

    private MafMessagePayload CreateUserMessage(string text)
    {
        return new MafMessagePayload
        {
            Id = GenerateId("msg"),
            Type = "message",
            Side = "right",
            SenderType = "user",
            SenderName = "You",
            Icon = "user",
            BubbleStyle = "user",
            SystemStyle = null,
            Text = text,
            Tools = Array.Empty<object>(),
            CreatedAt = DateTime.UtcNow,
            // Always mirror the user's own message into the split view so the chat history
            // is preserved when the workflow flips into human-handoff mode.
            SplitMirror = true,
        };
    }

    private MafMessagePayload CreateAgentMessage(string text, AgentIdentity agent, IReadOnlyList<AgentToolCall>? tools)
    {
        return new MafMessagePayload
        {
            Id = GenerateId("msg"),
            Type = "message",
            Side = "left",
            SenderType = "agent",
            SenderName = agent.Name,
            Icon = agent.Icon,
            BubbleStyle = agent.BubbleStyle,
            SystemStyle = null,
            Text = text,
            Tools = (tools ?? Array.Empty<AgentToolCall>())
                .Select(t => (object)new MafToolCallPayload { Name = t.Name, Args = t.Args, Ok = t.Ok })
                .ToArray(),
            CreatedAt = DateTime.UtcNow,
            // Default agent messages to mirror so the human-handoff split chat keeps the full history.
            SplitMirror = true,
        };
    }

    private WorkflowSession CreateSession(string sessionId)
    {
        var interactor = new SessionWorkflowInteractor(sessionId, this);
        var workflow = _workflowFactory(interactor);
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
                await _parent.PublishTraceAsync(_sessionId, $"Session error: {ex.Message}", TraceConstants.IconSiren, TraceConstants.ColorError);
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


        public async Task SendUserResponseAsync(string prompt, AgentIdentity? agent = null, IReadOnlyList<AgentToolCall>? tools = null, CancellationToken cancellationToken = default)
        {
            if (IsConsolePrompt(prompt) && (tools is null || tools.Count == 0))
            {
                return;
            }

            var resolved = ResolveAgent(agent);
            await _parent.PublishMessageAsync(_sessionId, _parent.CreateAgentMessage(prompt, resolved, tools));
        }

        public async Task<string> GetUserResponseAsync(string prompt, AgentIdentity? agent = null, IReadOnlyList<AgentToolCall>? tools = null, CancellationToken cancellationToken = default)
        {
            // Some executors call GetUserResponseAsync purely to read the next user/human input
            // and pass a console-style placeholder prompt (e.g. "[ATENDENTE HUMANO] "). Those
            // placeholders should NOT show up as agent chat bubbles in the UI.
            if (!IsConsolePrompt(prompt))
            {
                var resolved = ResolveAgent(agent);
                await _parent.PublishMessageAsync(_sessionId, _parent.CreateAgentMessage(prompt, resolved, tools));
            }

            return await _incomingMessages.Reader.ReadAsync(cancellationToken);
        }

        private static bool IsConsolePrompt(string? prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
            {
                return true;
            }

            var trimmed = prompt.Trim();
            return trimmed.StartsWith("[ATENDENTE", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("[USUÁRIO", StringComparison.OrdinalIgnoreCase)
                || trimmed.StartsWith("[SISTEMA", StringComparison.OrdinalIgnoreCase);
        }

        public async Task SetAgentTypingAsync(string label, bool on, CancellationToken cancellationToken = default)
        {
            await _parent.PublishTypingAsync(_sessionId, on, label);
        }

        public async Task PublishTraceAsync(string title, string icon = "terminal", string color = "primary", CancellationToken cancellationToken = default)
        {
            await _parent.PublishTraceAsync(_sessionId, title, icon, color);
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
            var payload = (items ?? Array.Empty<KbEntry>()).Select(item => new MafKbPayload
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
        if (outputData is TriageResult triageResult)
        {
            await PublishMessageAsync(sessionId, CreateAgentMessage(
                string.IsNullOrWhiteSpace(triageResult.Summary) ? "Issue classified." : triageResult.Summary,
                AgentRegistry["triage"], null));
            return;
        }

        if (outputData is FrequentProblemResult frequentProblemResult)
        {
            await PublishMessageAsync(sessionId, CreateAgentMessage(
                frequentProblemResult.MessageForUser ?? "Analyzing issue against known problems.",
                AgentRegistry["freq"], null));

            if (frequentProblemResult.MatchedIssue != null)
            {
                var kbEntry = new MafKbPayload
                {
                    Id = GenerateId("kb"),
                    Title = frequentProblemResult.MatchedIssue.Problem,
                    Category = string.Empty,
                    Score = frequentProblemResult.MatchedIssue.SuccessRate,
                    Summary = frequentProblemResult.MatchedIssue.Symptoms.FirstOrDefault() ?? frequentProblemResult.MatchedIssue.Solution ?? string.Empty,
                    ResolutionType = frequentProblemResult.MatchedIssue.McpAction ?? "knowledge-base",
                    Tags = frequentProblemResult.MatchedIssue.Keywords.ToArray()
                };
                await PublishKbAsync(sessionId, new[] { kbEntry });
            }
            return;
        }

        if (outputData is ResolutionResult resolutionResult)
        {
            var tools = (resolutionResult.ActionsExecuted ?? new List<string>())
                .Where(action => !string.IsNullOrWhiteSpace(action))
                .Select(action => new MafToolCallPayload
                {
                    Name = action,
                    Args = string.Empty,
                    Ok = resolutionResult.IsResolved,
                })
                .Cast<object>()
                .ToArray();

            await PublishMessageAsync(sessionId, new MafMessagePayload
            {
                Id = GenerateId("msg"),
                Type = "message",
                Side = "left",
                SenderType = "agent",
                SenderName = AgentRegistry["res"].Name,
                Icon = AgentRegistry["res"].Icon,
                BubbleStyle = AgentRegistry["res"].BubbleStyle,
                SystemStyle = null,
                Text = resolutionResult.MessageForUser ?? "Resolution completed.",
                Tools = tools,
                CreatedAt = DateTime.UtcNow,
                SplitMirror = true,
            });
            return;
        }

        if (outputData is PatternRecordResult patternRecordResult)
        {
            await PublishMessageAsync(sessionId, CreateAgentMessage(
                patternRecordResult.PatternDescription ?? "Pattern recorded.",
                AgentRegistry["pattern"], null));
            return;
        }

        if (outputData is string text)
        {
            // Strings yielded by ResolutionExecutor / TriageExecutor are intermediate progress
            // text. The dedicated typed-output branches above already publish the canonical
            // agent message, so the string path is intentionally a no-op to avoid duplicated
            // "MAF Agent" rows in the chat.
            return;
        }

        if (outputData != null)
        {
            await PublishMessageAsync(sessionId, CreateAgentMessage(
                outputData.ToString() ?? string.Empty,
                AgentIdentity.Default, null));
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var session in _sessions.Values)
        {
            await session.DisposeAsync();
        }

        await _connection.DisposeAsync();
    }
}

internal sealed class MafEventEnvelope
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("eventType")]
    public string EventType { get; set; } = string.Empty;

    [JsonPropertyName("payload")]
    public object Payload { get; set; } = new { };

    [JsonPropertyName("occurredAt")]
    public DateTime OccurredAt { get; set; }

    [JsonPropertyName("sequenceId")]
    public string SequenceId { get; set; } = string.Empty;
}

internal sealed class MafMessagePayload
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; set; } = string.Empty;

    [JsonPropertyName("senderType")]
    public string SenderType { get; set; } = string.Empty;

    [JsonPropertyName("senderName")]
    public string SenderName { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("bubbleStyle")]
    public string BubbleStyle { get; set; } = string.Empty;

    [JsonPropertyName("systemStyle")]
    public string? SystemStyle { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("tools")]
    public object[] Tools { get; set; } = Array.Empty<object>();

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("splitMirror")]
    public bool SplitMirror { get; set; }
}

internal sealed class MafToolCallPayload
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("args")]
    public string Args { get; set; } = string.Empty;

    [JsonPropertyName("ok")]
    public bool Ok { get; set; } = true;
}

internal sealed class MafTracePayload
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("time")]
    public DateTime Time { get; set; }

    [JsonPropertyName("icon")]
    public string Icon { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string Color { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    [JsonPropertyName("level")]
    public string Level { get; set; } = string.Empty;
}

internal sealed class MafTypingPayload
{
    [JsonPropertyName("container")]
    public string Container { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("on")]
    public bool On { get; set; }
}

internal sealed class MafAgentPayload
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("tag")]
    public string Tag { get; set; } = string.Empty;

    [JsonPropertyName("activeTools")]
    public object[] ActiveTools { get; set; } = Array.Empty<object>();
}

internal sealed class MafContextPayload
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("chatTitle")]
    public string ChatTitle { get; set; } = string.Empty;

    [JsonPropertyName("chatSubtitle")]
    public string ChatSubtitle { get; set; } = string.Empty;

    [JsonPropertyName("activeAgentId")]
    public string ActiveAgentId { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("confidence")]
    public double? Confidence { get; set; }

    [JsonPropertyName("intent")]
    public string? Intent { get; set; }

    [JsonPropertyName("humanMode")]
    public bool HumanMode { get; set; }

    [JsonPropertyName("resolutionSteps")]
    public object[]? ResolutionSteps { get; set; }
}

internal sealed class MafKbPayload
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; set; } = string.Empty;

    [JsonPropertyName("score")]
    public double Score { get; set; }

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("resolutionType")]
    public string ResolutionType { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public string[] Tags { get; set; } = Array.Empty<string>();
}

internal sealed class MafStartWorkflowCommand
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("workflowId")]
    public string WorkflowId { get; set; } = string.Empty;

    [JsonPropertyName("ticketId")]
    public string TicketId { get; set; } = string.Empty;

    [JsonPropertyName("initialMessage")]
    public string? InitialMessage { get; set; }

    [JsonPropertyName("mafWorkflowName")]
    public string MafWorkflowName { get; set; } = string.Empty;

    [JsonPropertyName("mafWorkflowVersion")]
    public string MafWorkflowVersion { get; set; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public string InputSchema { get; set; } = string.Empty;
}

internal sealed class MafUserMessageCommand
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

internal sealed class MafHumanMessageCommand
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

internal sealed class MafRunScenarioCommand
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    [JsonPropertyName("scenarioId")]
    public string ScenarioId { get; set; } = string.Empty;
}

internal sealed class MafSessionCommand
{
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;
}
