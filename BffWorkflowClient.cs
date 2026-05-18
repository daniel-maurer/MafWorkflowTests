using System.Collections.Concurrent;
using System.Threading.Channels;
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
        await PublishAgentStateAsync(command.SessionId, "triage", "active", "Running");
        await session.EnqueueMessageAsync(command.Text);
    }

    private async Task HandleHumanMessageAsync(MafHumanMessageCommand command)
    {
        if (!_sessions.TryGetValue(command.SessionId, out var session))
        {
            Logger.LogWarning($"Received humanMessage for unknown session {command.SessionId}.");
            return;
        }

        await session.EnqueueMessageAsync(command.Text);
        await PublishMessageAsync(command.SessionId, new MafMessagePayload
        {
            Id = GenerateId("msg"),
            Type = "message",
            Side = "left",
            SenderType = "human",
            SenderName = "Human Agent",
            Icon = "person",
            BubbleStyle = "human",
            SystemStyle = null,
            Text = command.Text,
            Tools = Array.Empty<object>(),
            CreatedAt = DateTime.UtcNow,
            SplitMirror = true
        });
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

    private MafMessagePayload CreateUserMessage(string text)
    {
        return new MafMessagePayload
        {
            Id = GenerateId("msg"),
            Type = "message",
            Side = "right",
            SenderType = "user",
            SenderName = "User",
            Icon = "user",
            BubbleStyle = "user",
            SystemStyle = null,
            Text = text,
            Tools = Array.Empty<object>(),
            CreatedAt = DateTime.UtcNow,
            SplitMirror = false
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


        public async Task SendUserResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            await _parent.PublishMessageAsync(_sessionId, new MafMessagePayload
            {
                Id = GenerateId("msg"),
                Type = "message",
                Side = "left",
                SenderType = "agent",
                SenderName = "MAF Agent",
                Icon = "git-branch",
                BubbleStyle = "agent",
                SystemStyle = null,
                Text = prompt,
                Tools = Array.Empty<object>(),
                CreatedAt = DateTime.UtcNow,
                SplitMirror = false
            });
        }

        public async Task<string> GetUserResponseAsync(string prompt, CancellationToken cancellationToken = default)
        {
            await _parent.PublishMessageAsync(_sessionId, new MafMessagePayload
            {
                Id = GenerateId("msg"),
                Type = "message",
                Side = "left",
                SenderType = "agent",
                SenderName = "MAF Agent",
                Icon = "git-branch",
                BubbleStyle = "agent",
                SystemStyle = null,
                Text = prompt,
                Tools = Array.Empty<object>(),
                CreatedAt = DateTime.UtcNow,
                SplitMirror = false
            });

            return await _incomingMessages.Reader.ReadAsync(cancellationToken);
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
            await PublishMessageAsync(sessionId, new MafMessagePayload
            {
                Id = GenerateId("msg"),
                Type = "message",
                Side = "left",
                SenderType = "agent",
                SenderName = "Triage Agent",
                Icon = "git-branch",
                BubbleStyle = "triage",
                SystemStyle = null,
                Text = triageResult.Summary,
                Tools = Array.Empty<object>(),
                CreatedAt = DateTime.UtcNow,
                SplitMirror = false
            });
            return;
        }

        if (outputData is FrequentProblemResult frequentProblemResult)
        {
            await PublishMessageAsync(sessionId, new MafMessagePayload
            {
                Id = GenerateId("msg"),
                Type = "message",
                Side = "left",
                SenderType = "agent",
                SenderName = "KB Agent",
                Icon = "search",
                BubbleStyle = "kb",
                SystemStyle = null,
                Text = frequentProblemResult.MessageForUser ?? "Analyzing issue against known problems.",
                Tools = Array.Empty<object>(),
                CreatedAt = DateTime.UtcNow,
                SplitMirror = false
            });
            return;
        }

        if (outputData is ResolutionResult resolutionResult)
        {
            await PublishMessageAsync(sessionId, new MafMessagePayload
            {
                Id = GenerateId("msg"),
                Type = "message",
                Side = "left",
                SenderType = "agent",
                SenderName = "Resolution Agent",
                Icon = "check-circle",
                BubbleStyle = "resolution",
                SystemStyle = null,
                Text = resolutionResult.MessageForUser ?? "Resolution completed.",
                Tools = Array.Empty<object>(),
                CreatedAt = DateTime.UtcNow,
                SplitMirror = false
            });
        }

        if (outputData is string text)
        {
            await PublishMessageAsync(sessionId, new MafMessagePayload
            {
                Id = GenerateId("msg"),
                Type = "message",
                Side = "left",
                SenderType = "agent",
                SenderName = "MAF Agent",
                Icon = "git-branch",
                BubbleStyle = "agent",
                SystemStyle = null,
                Text = text,
                Tools = Array.Empty<object>(),
                CreatedAt = DateTime.UtcNow,
                SplitMirror = false
            });
            return;
        }

        if (outputData is PatternRecordResult patternRecordResult)
        {
            await PublishMessageAsync(sessionId, new MafMessagePayload
            {
                Id = GenerateId("msg"),
                Type = "message",
                Side = "left",
                SenderType = "agent",
                SenderName = "Pattern Record Agent",
                Icon = "bar-chart-2",
                BubbleStyle = "pattern",
                SystemStyle = null,
                Text = patternRecordResult.PatternDescription ?? "Pattern recorded.",
                Tools = Array.Empty<object>(),
                CreatedAt = DateTime.UtcNow,
                SplitMirror = false
            });
            return;
        }

        if (outputData != null)
        {
            await PublishMessageAsync(sessionId, new MafMessagePayload
            {
                Id = GenerateId("msg"),
                Type = "message",
                Side = "left",
                SenderType = "agent",
                SenderName = "MAF Agent",
                Icon = "git-branch",
                BubbleStyle = "agent",
                SystemStyle = null,
                Text = outputData.ToString() ?? string.Empty,
                Tools = Array.Empty<object>(),
                CreatedAt = DateTime.UtcNow,
                SplitMirror = false
            });
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
