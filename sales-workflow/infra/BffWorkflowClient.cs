using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Channels;
using Microsoft.Agents.AI.Workflows;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.AI;
using SalesWorkflow.Models;
using SalesWorkflow.Services;

namespace SalesWorkflow;

internal sealed class BffWorkflowClient : IAsyncDisposable
{
    private readonly WorkflowConfiguration _configuration;
    private readonly IChatClient _chatClient;
    private readonly SalesAdminClient _salesAdminClient;
    private readonly InstructionCache _instructionCache;
    private readonly Func<IUserInteractor, Workflow> _workflowFactory;
    private readonly HubConnection _connection;
    private readonly ConcurrentDictionary<string, WorkflowSession> _sessions = new();
    private readonly ConcurrentDictionary<string, (string Text, DateTime Timestamp)> _lastUserMessages = new();

    public BffWorkflowClient(
        WorkflowConfiguration configuration,
        IChatClient chatClient,
        SalesAdminClient salesAdminClient,
        InstructionCache instructionCache,
        Func<IUserInteractor, Workflow> workflowFactory)
    {
        _configuration = configuration;
        _chatClient = chatClient;
        _salesAdminClient = salesAdminClient;
        _instructionCache = instructionCache;
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

        CustomerInfo? customer = null;
        if (!string.IsNullOrWhiteSpace(command.CustomerId))
        {
            customer = await _salesAdminClient.FindCustomerAsync(command.CustomerId);
        }
        else if (!string.IsNullOrWhiteSpace(command.CustomerData))
        {
            try
            {
                customer = JsonSerializer.Deserialize<CustomerInfo>(command.CustomerData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { }
        }

        if (customer != null)
        {
            session.Interactor.CurrentCustomer = customer;
            await PublishTraceAsync(command.SessionId, $"Cliente identificado: {customer.Name} (ID: {customer.Id})", "info");
        }
        else
        {
            session.Interactor.CurrentCustomer = null;
            await PublishTraceAsync(command.SessionId, "Nenhum cliente pré-identificado. O assistente solicitará os dados durante o atendimento.", "info");
        }

        var startMsg = !string.IsNullOrWhiteSpace(command.InitialMessage)
            ? command.InitialMessage
            : string.Empty;

        await session.StartAsync(startMsg);

        if (!string.IsNullOrWhiteSpace(startMsg))
        {
            await PublishMessageAsync(command.SessionId, CreateUserMessage(startMsg));
            await PublishTraceAsync(command.SessionId, "Fluxo comercial MAF iniciado com mensagem.");
            await PublishAgentStateAsync(command.SessionId, "intent", "active", "Running");
            await PublishContextAsync(command.SessionId, new MafContextPayload
            {
                Status = "analyzing-intent",
                ChatTitle = "Sales Assistant",
                ChatSubtitle = customer != null ? $"Cliente: {customer.Name}" : "Identificando intenção...",
                ActiveAgentId = "intent",
                HumanMode = false
            });
        }
        else
        {
            await PublishTraceAsync(command.SessionId, "Sessão comercial iniciada. Aguardando mensagem do cliente.");
            await PublishContextAsync(command.SessionId, new MafContextPayload
            {
                Status = "idle",
                ChatTitle = "Sales Assistant",
                ChatSubtitle = customer != null ? $"Cliente: {customer.Name}" : "Aguardando mensagem...",
                ActiveAgentId = string.Empty,
                HumanMode = false
            });
        }
    }

    private async Task HandleUserMessageAsync(MafUserMessageCommand command)
    {
        if (!_sessions.TryGetValue(command.SessionId, out var session) || session.IsCompleted)
        {
            CustomerInfo? existingCustomer = session?.Interactor.CurrentCustomer;
            if (session != null)
            {
                _sessions.TryRemove(command.SessionId, out _);
                await session.DisposeAsync();
            }

            Logger.LogInfo($"[SessionRecovery] Sessão {command.SessionId} iniciando nova execução para: '{command.Text}'");
            session = _sessions.GetOrAdd(command.SessionId, id => CreateSession(id, "sales-assistant"));
            if (existingCustomer != null)
            {
                session.Interactor.CurrentCustomer = existingCustomer;
            }
            await session.StartAsync(command.Text);

            if (!command.Text.StartsWith("__START_"))
            {
                await PublishMessageAsync(command.SessionId, CreateUserMessage(command.Text));
            }
            await PublishTraceAsync(command.SessionId, "Fluxo comercial MAF reiniciado para responder ao cliente.");
            await PublishAgentStateAsync(command.SessionId, "intent", "active", "Running");
            await PublishContextAsync(command.SessionId, new MafContextPayload
            {
                Status = "analyzing-intent",
                ChatTitle = "Sales Assistant",
                ChatSubtitle = existingCustomer != null ? $"Cliente: {existingCustomer.Name}" : "Identificando intenção e catálogo...",
                ActiveAgentId = "intent",
                HumanMode = false
            });
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

        if (!command.Text.StartsWith("__START_"))
        {
            await PublishMessageAsync(command.SessionId, CreateUserMessage(command.Text));
        }
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
            Logger.LogInfo($"[SessionRecovery] Sessão {command.SessionId} recuperada para runScenario.");
            session = _sessions.GetOrAdd(command.SessionId, id => CreateSession(id, "sales-assistant"));
            await session.StartAsync(command.ScenarioId);
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

        CustomerInfo? customer = null;
        if (!string.IsNullOrWhiteSpace(command.CustomerId))
        {
            customer = await _salesAdminClient.FindCustomerAsync(command.CustomerId);
        }
        else if (!string.IsNullOrWhiteSpace(command.CustomerData))
        {
            try
            {
                customer = JsonSerializer.Deserialize<CustomerInfo>(command.CustomerData, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch { }
        }

        var newSession = _sessions.GetOrAdd(command.SessionId, id => CreateSession(id, "sales-assistant"));
        if (customer != null)
        {
            newSession.Interactor.CurrentCustomer = customer;
            await PublishTraceAsync(command.SessionId, $"Sessão reiniciada com cliente: {customer.Name} (ID: {customer.Id})", "info");
        }
        else
        {
            newSession.Interactor.CurrentCustomer = null;
            await PublishTraceAsync(command.SessionId, "Sessão reiniciada sem cliente identificado.", "info");
        }

        await newSession.StartAsync(string.Empty);

        await PublishContextAsync(command.SessionId, new MafContextPayload
        {
            Status = "idle",
            ChatTitle = "Workflow reiniciado",
            ChatSubtitle = customer != null ? $"Cliente: {customer.Name}" : "Sessão reinicializada.",
            ActiveAgentId = string.Empty,
            HumanMode = false
        });
    }

    private async Task PublishMessageAsync(string sessionId, MafMessagePayload payload)
    {
        await PublishPublicEventAsync(sessionId, "message", payload);
        _ = SyncMessageToAdminAsync(sessionId, payload);
    }

    private async Task SyncMessageToAdminAsync(string sessionId, MafMessagePayload payload)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(payload.Text))
            {
                return;
            }

            if (payload.Text.StartsWith("__START_"))
            {
                return;
            }

            string role = payload.SenderType switch
            {
                "user" => "user",
                "human" => "human",
                "system" => "system",
                "agent" => !string.IsNullOrWhiteSpace(payload.AgentId) ? payload.AgentId : "assistant",
                _ => payload.SenderType ?? "assistant"
            };

            Guid? customerId = null;
            if (_sessions.TryGetValue(sessionId, out var session) && session.Interactor.CurrentCustomer != null)
            {
                if (Guid.TryParse(session.Interactor.CurrentCustomer.Id, out var cid))
                {
                    customerId = cid;
                }
            }

            var req = new SyncConversationMessageRequest
            {
                SessionId = sessionId,
                CustomerId = customerId,
                Role = role,
                Content = payload.Text,
                Status = "active"
            };

            await _salesAdminClient.SyncConversationMessageAsync(req);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"[AdminSync] Falha ao sincronizar mensagem para Admin: {ex.Message}");
        }
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
            SplitMirror = true,
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
            "sales-assistant" => WorkflowFactory.BuildSalesWorkflow(_chatClient, interactor, _salesAdminClient, _instructionCache),
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

        public SessionWorkflowInteractor Interactor => _userInteractor;
        public bool IsCompleted => _runnerTask.IsCompleted;
    }

    private sealed class SessionWorkflowInteractor : ISalesUserInteractor
    {
        private readonly string _sessionId;
        private readonly BffWorkflowClient _parent;
        private readonly Channel<string> _incomingMessages = Channel.CreateUnbounded<string>();

        public string SessionId => _sessionId;
        public CustomerInfo? CurrentCustomer { get; set; }

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

