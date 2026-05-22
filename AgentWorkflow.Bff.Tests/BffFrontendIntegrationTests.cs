using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using AgentWorkflow.Bff.Contracts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgentWorkflow.Bff.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Resolve workflows configuration path relative to the test project root
        var workflowsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../agent-workflow-bff/Workflows"));
        
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "WorkflowConfig:Folder", workflowsPath }
            });
        });
    }
}

public class BffFrontendIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncDisposable
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly List<HubConnection> _connections = new();

    public BffFrontendIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    private async Task<HubConnection> CreateHubConnectionAsync(string path, string token)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(_factory.Server.BaseAddress, path), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(token)!;
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();

        await connection.StartAsync();
        _connections.Add(connection);
        return connection;
    }

    [Fact]
    public async Task Scenario1_KnownProblem_ResolvesAutomatically()
    {
        // 1. Setup HttpClient for REST APIs
        using var httpClient = _factory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "mock-token:developer");

        // 2. Connect a simulated MAF Worker Client
        var mafConnection = await CreateHubConnectionAsync("hubs/maf", "mock-token:maf-worker");

        // Register the worker for the "support" workflow
        await mafConnection.InvokeAsync("RegisterWorker", "worker-1", new[] { "support" });

        // Setup TCS to wait for the workflow to be started by the BFF
        var startWorkflowTcs = new TaskCompletionSource<MafStartWorkflowCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
        mafConnection.On<MafStartWorkflowCommand>("startWorkflow", cmd =>
        {
            startWorkflowTcs.TrySetResult(cmd);
        });

        // 3. POST /api/workflow-sessions to create a session
        var request = new CreateWorkflowSessionRequest("support", "Quero redefinir minha senha");
        var response = await httpClient.PostAsJsonAsync("/api/workflow-sessions", request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception($"HTTP {response.StatusCode}: {body}");
        }

        var sessionResponse = await response.Content.ReadFromJsonAsync<CreateWorkflowSessionResponse>();
        Assert.NotNull(sessionResponse);
        var sessionId = sessionResponse.SessionId;
        Assert.NotEmpty(sessionId);
        Assert.NotEmpty(sessionResponse.TicketId);

        // 4. Verify MAF worker received the start workflow command
        var startCommand = await startWorkflowTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(sessionId, startCommand.SessionId);
        Assert.Equal("support", startCommand.WorkflowId);
        Assert.Equal("Quero redefinir minha senha", startCommand.InitialMessage);

        // 5. Connect simulated Frontend Client to SignalR
        var frontendConnection = await CreateHubConnectionAsync("hubs/workflow", "mock-token:developer");

        var receivedMessages = new List<MessageDto>();
        var receivedTraces = new List<TraceEventDto>();
        var receivedAgents = new List<AgentRuntimeStateDto>();
        var receivedKb = new List<IReadOnlyList<KbItemDto>>();
        var receivedContexts = new List<JsonElement>();
        var receivedTypings = new List<(string Container, string Label, bool On)>();

        // Register callbacks
        frontendConnection.On<string, MessageDto>("message", (sid, msg) => receivedMessages.Add(msg));
        frontendConnection.On<string, TraceEventDto>("trace", (sid, trc) => receivedTraces.Add(trc));
        frontendConnection.On<string, AgentRuntimeStateDto>("agent", (sid, ag) => receivedAgents.Add(ag));
        frontendConnection.On<string, IReadOnlyList<KbItemDto>>("kb", (sid, kb) => receivedKb.Add(kb));
        frontendConnection.On<string, JsonElement>("context", (sid, ctx) => receivedContexts.Add(ctx));
        frontendConnection.On<string, string, string, bool>("typing", (sid, container, label, on) => receivedTypings.Add((container, label, on)));

        // Join session
        await frontendConnection.InvokeAsync("JoinSession", sessionId);

        // Verify initial context setup was received by frontend client
        await Task.Delay(200);
        Assert.NotEmpty(receivedContexts);
        Assert.NotEmpty(receivedAgents);

        // 6. Simulate MAF worker publishing events representing Scenario 1 (Known Problem Resolution)
        var occurredAt = DateTimeOffset.UtcNow;

        // Trace: Workflow Started
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "trace",
            new TraceEventDto("trc_001", occurredAt, "git-branch", "primary", "Workflow started.", null, "primary"),
            occurredAt));

        // Typing: Triage Agent typing
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "typing",
            new TypingEventDto("msgs", "Triage Agent analisando...", true),
            occurredAt));

        // Agent: Triage Active
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "agent",
            new AgentRuntimeStateDto("triage", "active", "Running", Array.Empty<string>()),
            occurredAt));

        // Typing: Triage Agent stopped typing
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "typing",
            new TypingEventDto("msgs", "Triage Agent analisando...", false),
            occurredAt));

        // Message: Triage Result (Audience: Attendant)
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "message",
            new MessageDto("msg_001", "message", "left", "agent", "Triage Agent", "git-branch", "triage", null, "Problema classificado como Redefinição de Senha.", Array.Empty<ToolCallDto>(), occurredAt, true, "attendant"),
            occurredAt));

        // Agent Transition: Triage Completed, Freq Active
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "agent",
            new AgentRuntimeStateDto("triage", "done", "Done", Array.Empty<string>()),
            occurredAt));
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "agent",
            new AgentRuntimeStateDto("freq", "active", "Running", Array.Empty<string>()),
            occurredAt));

        // KB Update
        var kbItem = new KbItemDto("kb_001", "Login failure — password reset", "Auth", 0.97m, "User unable to access account.", "password-reset", new[] { "login", "password" });
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "kb",
            new[] { kbItem },
            occurredAt));

        // Agent Transition: Freq Completed, Resolution Active
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "agent",
            new AgentRuntimeStateDto("freq", "done", "Done", Array.Empty<string>()),
            occurredAt));
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "agent",
            new AgentRuntimeStateDto("res", "active", "Running", new[] { "reset_password" }),
            occurredAt));

        // Message: Resolution Result (Audience: Both, with tool calls executed)
        var toolCalls = new[] { new ToolCallDto("reset_password", "{}", true) };
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "message",
            new MessageDto("msg_002", "message", "left", "agent", "Resolution Agent", "wrench", "res", null, "Sua senha foi redefinida com sucesso. Verifique seu e-mail.", toolCalls, occurredAt, true, "both"),
            occurredAt));

        // Agent Transition: Resolution Completed, Pattern Active
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "agent",
            new AgentRuntimeStateDto("res", "done", "Done", Array.Empty<string>()),
            occurredAt));
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "agent",
            new AgentRuntimeStateDto("pattern", "active", "Running", Array.Empty<string>()),
            occurredAt));

        // Context: Status Completed/Resolved
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "context",
            new { status = "resolved", chatTitle = "Resolvido", activeAgentId = "" },
            occurredAt));

        // 7. Verify all events were received by the Frontend Client
        await Task.Delay(500); // Give time for SignalR delivery

        // Verify Traces
        var trace = Assert.Single(receivedTraces);
        Assert.Equal("trc_001", trace.Id);
        Assert.Equal("Workflow started.", trace.Title);

        // Verify Typing Indicators (both On and Off events)
        Assert.Contains(receivedTypings, t => t.Label == "Triage Agent analisando..." && t.On);
        Assert.Contains(receivedTypings, t => t.Label == "Triage Agent analisando..." && !t.On);

        // Verify Agent Transitions
        Assert.Contains(receivedAgents, a => a.Id == "triage" && a.State == "active");
        Assert.Contains(receivedAgents, a => a.Id == "triage" && a.State == "done");
        Assert.Contains(receivedAgents, a => a.Id == "freq" && a.State == "active");
        Assert.Contains(receivedAgents, a => a.Id == "res" && a.State == "active" && a.ActiveTools.Contains("reset_password"));
        Assert.Contains(receivedAgents, a => a.Id == "pattern" && a.State == "active");

        // Verify KB
        Assert.NotEmpty(receivedKb);
        Assert.Contains(receivedKb, list => list.Any(k => k.Id == "kb_001" && k.Title == "Login failure — password reset"));

        // Verify Messages
        Assert.Equal(2, receivedMessages.Count);
        // Message 1 (Triage result)
        Assert.Equal("msg_001", receivedMessages[0].Id);
        Assert.Equal("attendant", receivedMessages[0].Audience);
        // Message 2 (Resolution result)
        Assert.Equal("msg_002", receivedMessages[1].Id);
        Assert.Equal("both", receivedMessages[1].Audience);
        Assert.Single(receivedMessages[1].Tools!);
        Assert.Equal("reset_password", receivedMessages[1].Tools![0].Name);
        Assert.True(receivedMessages[1].Tools![0].Ok);

        // Verify Context status update
        Assert.Contains(receivedContexts, c => c.GetProperty("status").GetString() == "resolved");
    }

    [Fact]
    public async Task Scenario2_UnknownProblem_EscalatesToHumanSupport_ResolvedByHuman()
    {
        // 1. Setup HttpClient for REST APIs
        using var httpClient = _factory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "mock-token:developer");

        // 2. Connect a simulated MAF Worker Client
        var mafConnection = await CreateHubConnectionAsync("hubs/maf", "mock-token:maf-worker");

        // Register the worker
        await mafConnection.InvokeAsync("RegisterWorker", "worker-1", new[] { "support" });

        // Setup TCS to wait for the workflow to start
        var startWorkflowTcs = new TaskCompletionSource<MafStartWorkflowCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
        mafConnection.On<MafStartWorkflowCommand>("startWorkflow", cmd =>
        {
            startWorkflowTcs.TrySetResult(cmd);
        });

        // 3. POST /api/workflow-sessions to create a session
        var request = new CreateWorkflowSessionRequest("support", "Erro customizado 0xTISS-4821 na integracao");
        var response = await httpClient.PostAsJsonAsync("/api/workflow-sessions", request);
        response.EnsureSuccessStatusCode();

        var sessionResponse = await response.Content.ReadFromJsonAsync<CreateWorkflowSessionResponse>();
        Assert.NotNull(sessionResponse);
        var sessionId = sessionResponse.SessionId;

        // 4. Verify MAF worker receives start workflow command
        await startWorkflowTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));

        // 5. Connect Frontend Client
        var frontendConnection = await CreateHubConnectionAsync("hubs/workflow", "mock-token:developer");

        var receivedMessages = new List<MessageDto>();
        var receivedTraces = new List<TraceEventDto>();
        var receivedAgents = new List<AgentRuntimeStateDto>();
        var receivedSplitModes = new List<bool>();
        var receivedContexts = new List<JsonElement>();

        frontendConnection.On<string, MessageDto>("message", (sid, msg) => receivedMessages.Add(msg));
        frontendConnection.On<string, TraceEventDto>("trace", (sid, trc) => receivedTraces.Add(trc));
        frontendConnection.On<string, AgentRuntimeStateDto>("agent", (sid, ag) => receivedAgents.Add(ag));
        frontendConnection.On<string, bool>("splitMode", (sid, mode) => receivedSplitModes.Add(mode));
        frontendConnection.On<string, JsonElement>("context", (sid, ctx) => receivedContexts.Add(ctx));

        // Join session
        await frontendConnection.InvokeAsync("JoinSession", sessionId);

        // 6. Simulate MAF worker executing, finding no KB match, and escalating to human support
        var occurredAt = DateTimeOffset.UtcNow;

        // Escalation messages
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "message",
            new MessageDto("msg_sys_1", "system", "center", "system", "System", "siren", null, "escalate", "Freq. Problem Agent → no KB match. Routing to human agent queue...", Array.Empty<ToolCallDto>(), occurredAt, true, "both"),
            occurredAt));

        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "message",
            new MessageDto("msg_sys_2", "system", "center", "system", "System", "user-check", null, "handoff", "Human agent Daniel M. assigned. Joining the conversation now.", Array.Empty<ToolCallDto>(), occurredAt, true, "both"),
            occurredAt));

        // Activate splitMode
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "splitMode",
            true,
            occurredAt));

        // Agent: human-support Active
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "agent",
            new AgentRuntimeStateDto("human-support", "active", "Running", Array.Empty<string>()),
            occurredAt));

        // Context: human-chat status
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "context",
            new { status = "human-chat", chatTitle = "Human handoff", activeAgentId = "human-support", humanMode = true },
            occurredAt));

        // Trace: Human agent joined
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "trace",
            new TraceEventDto("trc_h_001", occurredAt, "user-check", "primary", "Human agent Daniel M. joined the conversation (no KB match)", null, "primary"),
            occurredAt));

        await Task.Delay(200); // Wait for delivery

        // Assert escalation occurred
        Assert.Contains(receivedSplitModes, mode => mode);
        Assert.Contains(receivedAgents, a => a.Id == "human-support" && a.State == "active");
        Assert.Contains(receivedContexts, c => c.GetProperty("status").GetString() == "human-chat");

        // 7. Simulate Human Operator sending a message to client
        var humanMessageTcs = new TaskCompletionSource<MafHumanMessageCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
        mafConnection.On<MafHumanMessageCommand>("humanMessage", cmd =>
        {
            humanMessageTcs.TrySetResult(cmd);
        });

        // Frontend Client invokes SendHumanMessage
        await frontendConnection.InvokeAsync("SendHumanMessage", sessionId, "Olá, como posso ajudar com sua integração?");

        // Verify MAF Worker receives humanMessage command
        var humanCmd = await humanMessageTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Olá, como posso ajudar com sua integração?", humanCmd.Text);

        // MAF worker publishes the message back
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "message",
            new MessageDto("msg_human_reply", "message", "left", "human", "Daniel M.", "headphones", "human", null, "Olá, como posso ajudar com sua integração?", Array.Empty<ToolCallDto>(), occurredAt, true, "both"),
            occurredAt));

        // 8. Simulate User replying
        var userMessageTcs = new TaskCompletionSource<MafUserMessageCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
        mafConnection.On<MafUserMessageCommand>("userMessage", cmd =>
        {
            userMessageTcs.TrySetResult(cmd);
        });

        // Frontend Client invokes SendUserMessage
        await frontendConnection.InvokeAsync("SendUserMessage", sessionId, "Estou com erro ao tentar sincronizar.");

        // Verify MAF worker receives userMessage command
        var userCmd = await userMessageTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Estou com erro ao tentar sincronizar.", userCmd.Text);

        // MAF worker publishes the message back
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "message",
            new MessageDto("msg_user_reply", "message", "right", "user", "You", "user", "user", null, "Estou com erro ao tentar sincronizar.", Array.Empty<ToolCallDto>(), occurredAt, true, "both"),
            occurredAt));

        // 9. Simulate Human/User marking the ticket as solved
        var markSolvedTcs = new TaskCompletionSource<MafSessionCommand>(TaskCreationOptions.RunContinuationsAsynchronously);
        mafConnection.On<MafSessionCommand>("markSolved", cmd =>
        {
            markSolvedTcs.TrySetResult(cmd);
        });

        // Frontend client invokes MarkSolved
        await frontendConnection.InvokeAsync("MarkSolved", sessionId);

        // Verify MAF worker receives markSolved command
        var solvedCmd = await markSolvedTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(sessionId, solvedCmd.SessionId);

        // MAF worker publishes resolution completion events
        // Trace: Resolved by human support
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "trace",
            new TraceEventDto("trc_h_002", occurredAt, "user-check", "success", "Issue resolved by human support", null, "success"),
            occurredAt));

        // Message: Marked resolved by customer
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "message",
            new MessageDto("msg_sys_3", "system", "center", "system", "System", "check-circle", null, "resolved", "Issue marked as resolved by the customer.", Array.Empty<ToolCallDto>(), occurredAt, true, "both"),
            occurredAt));

        // Deactivate human-support agent state and split mode
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "agent",
            new AgentRuntimeStateDto("human-support", "done", "Done", Array.Empty<string>()),
            occurredAt));

        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "splitMode",
            false,
            occurredAt));

        // Context: Status completed
        await mafConnection.InvokeAsync("PublishEvent", new MafWorkflowEventEnvelope(
            sessionId,
            "context",
            new { status = "resolved", chatTitle = "Resolvido", activeAgentId = "", humanMode = false },
            occurredAt));

        await Task.Delay(500); // Wait for delivery

        // 10. Verify final assertions on the Frontend Client
        // Verify messages (should contain human, user, system messages)
        Assert.Contains(receivedMessages, m => m.Id == "msg_human_reply" && m.SenderType == "human" && m.Text == "Olá, como posso ajudar com sua integração?");
        Assert.Contains(receivedMessages, m => m.Id == "msg_user_reply" && m.SenderType == "user" && m.Text == "Estou com erro ao tentar sincronizar.");
        Assert.Contains(receivedMessages, m => m.Id == "msg_sys_3" && m.Type == "system" && m.SystemStyle == "resolved");

        // Verify traces
        Assert.Contains(receivedTraces, t => t.Id == "trc_h_002" && t.Color == "success" && t.Title == "Issue resolved by human support");

        // Verify final splitMode is false
        Assert.Contains(receivedSplitModes, mode => !mode);

        // Verify final agent state
        Assert.Contains(receivedAgents, a => a.Id == "human-support" && a.State == "done");

        // Verify final context
        Assert.Contains(receivedContexts, c => c.GetProperty("status").GetString() == "resolved" && !c.GetProperty("humanMode").GetBoolean());
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }
    }
}
