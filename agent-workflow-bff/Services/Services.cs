using AgentWorkflow.Bff.Contracts;
using AgentWorkflow.Bff.Hubs;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using AgentWorkflow.Bff.Options;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AgentWorkflow.Bff.Services;

public interface IWorkflowConfigStore
{
    Task<IReadOnlyList<WorkflowDefinitionDto>> GetAllAsync(CancellationToken cancellationToken);
    Task<WorkflowDefinitionDto?> GetByIdAsync(string workflowId, CancellationToken cancellationToken);
}

public interface ISessionRegistry
{
    SessionSnapshotDto Create(string workflowId);
    SessionSnapshotDto? GetSnapshot(string sessionId);
    SessionSnapshotDto? Reset(string sessionId);
    IReadOnlyList<MessageDto>? GetMessages(string sessionId, DateTimeOffset? since);
    IReadOnlyList<TraceEventDto>? GetTrace(string sessionId, DateTimeOffset? since);
    void ApplyMafEvent(MafWorkflowEventEnvelope envelope);
}

public interface IMafCommandPublisher
{
    Task StartWorkflowAsync(MafStartWorkflowCommand command, CancellationToken cancellationToken);
    Task SendUserMessageAsync(MafUserMessageCommand command, CancellationToken cancellationToken);
    Task SendHumanMessageAsync(MafHumanMessageCommand command, CancellationToken cancellationToken);
    Task RunScenarioAsync(MafRunScenarioCommand command, CancellationToken cancellationToken);
    Task MarkSolvedAsync(MafSessionCommand command, CancellationToken cancellationToken);
    Task ResetWorkflowAsync(MafSessionCommand command, CancellationToken cancellationToken);
}

public interface IFrontendEventPublisher
{
    Task PublishMafEventAsync(MafWorkflowEventEnvelope envelope, CancellationToken cancellationToken);
}

public sealed class JsonWorkflowConfigStore(IWebHostEnvironment env, IOptions<WorkflowConfigOptions> options) : IWorkflowConfigStore
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IReadOnlyList<WorkflowDefinitionDto>? _cache;

    public async Task<IReadOnlyList<WorkflowDefinitionDto>> GetAllAsync(CancellationToken cancellationToken)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_cache is not null)
            {
                return _cache;
            }

            var folder = Path.IsPathRooted(options.Value.Folder)
                ? options.Value.Folder
                : Path.Combine(env.ContentRootPath, options.Value.Folder);

            var files = Directory.Exists(folder)
                ? Directory.GetFiles(folder, "*.json", SearchOption.TopDirectoryOnly)
                : [];

            var workflows = new List<WorkflowDefinitionDto>();
            foreach (var file in files)
            {
                await using var stream = File.OpenRead(file);
                var workflow = await JsonSerializer.DeserializeAsync<WorkflowDefinitionDto>(stream, JsonOptions, cancellationToken);
                if (workflow is not null)
                {
                    workflows.Add(workflow);
                }
            }

            _cache = workflows.OrderBy(item => item.Title).ToArray();
            return _cache;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<WorkflowDefinitionDto?> GetByIdAsync(string workflowId, CancellationToken cancellationToken) =>
        (await GetAllAsync(cancellationToken)).FirstOrDefault(item => item.Id.Equals(workflowId, StringComparison.OrdinalIgnoreCase));

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}

public sealed class InMemorySessionRegistry(IWorkflowConfigStore configs) : ISessionRegistry
{
    private readonly ConcurrentDictionary<string, SessionSnapshotDto> _sessions = new();

    public SessionSnapshotDto Create(string workflowId)
    {
        var sessionId = $"ses_{Guid.NewGuid():N}";
        var ticketId = $"#TKT-{Random.Shared.Next(1000, 9999)}";

        var workflow = configs.GetByIdAsync(workflowId, CancellationToken.None).GetAwaiter().GetResult()
            ?? throw new InvalidOperationException($"WORKFLOW_NOT_FOUND: {workflowId}");

        var snapshot = new SessionSnapshotDto(
            sessionId,
            workflowId,
            ticketId,
            "idle",
            workflow.Title,
            workflow.Subtitle ?? "Ready",
            null,
            null,
            null,
            null,
            false,
            null,
            [],
            workflow.Agents.OrderBy(agent => agent.Order).Select(agent => new AgentRuntimeStateDto(agent.Id, "idle", "Idle", [])).ToArray(),
            [],
            [],
            []);

        _sessions[sessionId] = snapshot;
        return snapshot;
    }

    public SessionSnapshotDto? GetSnapshot(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var snapshot) ? snapshot : null;

    public SessionSnapshotDto? Reset(string sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var current))
        {
            return null;
        }

        var workflow = configs.GetByIdAsync(current.WorkflowId, CancellationToken.None).GetAwaiter().GetResult()
            ?? throw new InvalidOperationException($"WORKFLOW_NOT_FOUND: {current.WorkflowId}");

        var snapshot = new SessionSnapshotDto(
            sessionId,
            current.WorkflowId,
            current.TicketId,
            "idle",
            workflow.Title,
            workflow.Subtitle ?? "Ready",
            null,
            null,
            null,
            null,
            false,
            null,
            [],
            workflow.Agents.OrderBy(agent => agent.Order).Select(agent => new AgentRuntimeStateDto(agent.Id, "idle", "Idle", [])).ToArray(),
            [],
            [],
            []);

        _sessions[sessionId] = snapshot;
        return snapshot;
    }

    public IReadOnlyList<MessageDto>? GetMessages(string sessionId, DateTimeOffset? since)
    {
        if (!_sessions.TryGetValue(sessionId, out var snapshot))
        {
            return null;
        }

        return since is null
            ? snapshot.Messages
            : snapshot.Messages.Where(item => item.CreatedAt > since).ToArray();
    }

    public IReadOnlyList<TraceEventDto>? GetTrace(string sessionId, DateTimeOffset? since)
    {
        if (!_sessions.TryGetValue(sessionId, out var snapshot))
        {
            return null;
        }

        return since is null
            ? snapshot.Trace
            : snapshot.Trace.Where(item => item.Time > since).ToArray();
    }

    public void ApplyMafEvent(MafWorkflowEventEnvelope envelope)
    {
        if (!_sessions.TryGetValue(envelope.SessionId, out var snapshot))
        {
            return;
        }

        _sessions[envelope.SessionId] = envelope.EventType switch
        {
            "message" when envelope.Payload is JsonElement json => EnrichAndAppendMessage(snapshot, json),
            "trace" when envelope.Payload is JsonElement json => EnrichAndAppendTrace(snapshot, json),
            "agent" when envelope.Payload is JsonElement json => UpsertAgent(snapshot, json.Deserialize<AgentRuntimeStateDto>(JsonOptions)!),
            "kb" when envelope.Payload is JsonElement json => snapshot with { Kb = json.Deserialize<IReadOnlyList<KbItemDto>>(JsonOptions) ?? [] },
            "context" when envelope.Payload is JsonElement json => ApplyContextPatch(snapshot, json),
            "splitMode" when envelope.Payload is JsonElement json => snapshot with { HumanMode = json.GetBoolean() },
            _ => snapshot
        };
    }

    private static SessionSnapshotDto UpsertAgent(SessionSnapshotDto snapshot, AgentRuntimeStateDto agent)
    {
        var agents = snapshot.Agents.Where(item => item.Id != agent.Id).Append(agent).ToArray();
        return snapshot with { Agents = agents };
    }

    private static SessionSnapshotDto ApplyContextPatch(SessionSnapshotDto snapshot, JsonElement patch)
    {
        string? GetString(string name, string? fallback) =>
            patch.TryGetProperty(name, out var property) && property.ValueKind != JsonValueKind.Null ? property.GetString() : fallback;

        decimal? GetDecimal(string name, decimal? fallback) =>
            patch.TryGetProperty(name, out var property) && property.ValueKind != JsonValueKind.Null ? property.GetDecimal() : fallback;

        bool GetBool(string name, bool fallback) =>
            patch.TryGetProperty(name, out var property) && property.ValueKind != JsonValueKind.Null ? property.GetBoolean() : fallback;

        return snapshot with
        {
            Status = GetString("status", snapshot.Status)!,
            ChatTitle = GetString("chatTitle", snapshot.ChatTitle)!,
            ChatSubtitle = GetString("chatSubtitle", snapshot.ChatSubtitle)!,
            ActiveAgentId = GetString("activeAgentId", snapshot.ActiveAgentId),
            Category = GetString("category", snapshot.Category),
            Confidence = GetDecimal("confidence", snapshot.Confidence),
            Intent = GetString("intent", snapshot.Intent),
            HumanMode = GetBool("humanMode", snapshot.HumanMode)
        };
    }

    private SessionSnapshotDto EnrichAndAppendMessage(SessionSnapshotDto snapshot, JsonElement json)
    {
        var backend = json.Deserialize<BackendMessageDto>(JsonOptions);
        if (backend is null) return snapshot;

        var workflow = configs.GetByIdAsync(snapshot.WorkflowId, CancellationToken.None).GetAwaiter().GetResult();
        var enriched = TracePresentation.EnrichMessage(backend, workflow);
        return snapshot with { Messages = snapshot.Messages.Append(enriched).ToArray() };
    }

    private static SessionSnapshotDto EnrichAndAppendTrace(SessionSnapshotDto snapshot, JsonElement json)
    {
        var backend = json.Deserialize<BackendTraceEventDto>(JsonOptions);
        if (backend is null) return snapshot;

        var (icon, color) = TracePresentation.FromLevel(backend.Level);
        var enriched = new TraceEventDto(backend.Id, backend.Time, icon, color, backend.Title, null, backend.Level);
        return snapshot with { Trace = snapshot.Trace.Append(enriched).ToArray() };
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}

public sealed class MafCommandPublisher(IHubContext<MafBridgeHub> hubContext, ILogger<MafCommandPublisher> logger) : IMafCommandPublisher
{
    private readonly ILogger<MafCommandPublisher> _logger = logger;
    private readonly IHubContext<MafBridgeHub> _hubContext = hubContext;

    public Task StartWorkflowAsync(MafStartWorkflowCommand command, CancellationToken cancellationToken) =>
        LogAndSend(MafGroups.WorkflowWorkers(command.WorkflowId), "startWorkflow", command, cancellationToken);

    public Task SendUserMessageAsync(MafUserMessageCommand command, CancellationToken cancellationToken) =>
        LogAndSend(MafGroups.Workers, "userMessage", command, cancellationToken);

    public Task SendHumanMessageAsync(MafHumanMessageCommand command, CancellationToken cancellationToken) =>
        LogAndSend(MafGroups.Workers, "humanMessage", command, cancellationToken);

    public Task RunScenarioAsync(MafRunScenarioCommand command, CancellationToken cancellationToken) =>
        LogAndSend(MafGroups.Workers, "runScenario", command, cancellationToken);

    public Task MarkSolvedAsync(MafSessionCommand command, CancellationToken cancellationToken) =>
        LogAndSend(MafGroups.Workers, "markSolved", command, cancellationToken);

    public Task ResetWorkflowAsync(MafSessionCommand command, CancellationToken cancellationToken) =>
        LogAndSend(MafGroups.Workers, "resetWorkflow", command, cancellationToken);

    private Task LogAndSend(string groupName, string methodName, object payload, CancellationToken cancellationToken)
    {
        _logger.LogInformation("SignalR sending to MAF group={GroupName} method={MethodName} payload={Payload}", groupName, methodName, payload);
        return _hubContext.Clients.Group(groupName).SendAsync(methodName, payload, cancellationToken);
    }
}

public sealed class FrontendEventPublisher(
    IHubContext<FrontendWorkflowHub> hubContext,
    ISessionRegistry sessionRegistry,
    IWorkflowConfigStore configs,
    ILogger<FrontendEventPublisher> logger) : IFrontendEventPublisher
{
    private readonly ILogger<FrontendEventPublisher> _logger = logger;
    private readonly IHubContext<FrontendWorkflowHub> _hubContext = hubContext;

    public Task PublishMafEventAsync(MafWorkflowEventEnvelope envelope, CancellationToken cancellationToken)
    {
        _logger.LogInformation("SignalR publishing MAF event SessionId={SessionId} EventType={EventType} PayloadType={PayloadType}", envelope.SessionId, envelope.EventType, envelope.Payload?.GetType().Name);
        return envelope.EventType switch
        {
            "message" when envelope.Payload is not null => ForwardEnrichedMessageAsync(envelope.SessionId, envelope.Payload, cancellationToken),
            "trace" when envelope.Payload is not null => ForwardEnrichedTraceAsync(envelope.SessionId, envelope.Payload, cancellationToken),
            "agent" => _hubContext.Clients.Group(envelope.SessionId).SendAsync("agent", envelope.SessionId, envelope.Payload, cancellationToken),
            "kb" => _hubContext.Clients.Group(envelope.SessionId).SendAsync("kb", envelope.SessionId, envelope.Payload, cancellationToken),
            "context" => _hubContext.Clients.Group(envelope.SessionId).SendAsync("context", envelope.SessionId, envelope.Payload, cancellationToken),
            "splitMode" => _hubContext.Clients.Group(envelope.SessionId).SendAsync("splitMode", envelope.SessionId, envelope.Payload, cancellationToken),
            "typing" when envelope.Payload is JsonElement json => ForwardTypingAsync(envelope.SessionId, json.Deserialize<TypingEventDto>(JsonOptions)!, cancellationToken),
            _ => Task.CompletedTask
        };
    }

    private Task ForwardTypingAsync(string sessionId, TypingEventDto typing, CancellationToken cancellationToken) =>
        _hubContext.Clients.Group(sessionId).SendAsync("typing", sessionId, typing.Container, typing.Label, typing.On, cancellationToken);

    private Task ForwardEnrichedMessageAsync(string sessionId, object payload, CancellationToken cancellationToken)
    {
        if (payload is JsonElement json)
        {
            var backend = json.Deserialize<BackendMessageDto>(JsonOptions);
            if (backend is not null)
            {
                var snapshot = sessionRegistry.GetSnapshot(sessionId);
                var workflowId = snapshot?.WorkflowId ?? "support";
                var workflow = configs.GetByIdAsync(workflowId, cancellationToken).GetAwaiter().GetResult();
                var enriched = TracePresentation.EnrichMessage(backend, workflow);
                return _hubContext.Clients.Group(sessionId).SendAsync("message", sessionId, enriched, cancellationToken);
            }
        }
        return _hubContext.Clients.Group(sessionId).SendAsync("message", sessionId, payload, cancellationToken);
    }

    private Task ForwardEnrichedTraceAsync(string sessionId, object payload, CancellationToken cancellationToken)
    {
        if (payload is JsonElement json)
        {
            var backend = json.Deserialize<BackendTraceEventDto>(JsonOptions);
            if (backend is not null)
            {
                var (icon, color) = TracePresentation.FromLevel(backend.Level);
                var enriched = new TraceEventDto(backend.Id, backend.Time, icon, color, backend.Title, null, backend.Level);
                return _hubContext.Clients.Group(sessionId).SendAsync("trace", sessionId, enriched, cancellationToken);
            }
        }
        // Fallback: forward as-is (payload might already be a TraceEventDto)
        return _hubContext.Clients.Group(sessionId).SendAsync("trace", sessionId, payload, cancellationToken);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };
}

public static class MockBearerDefaults
{
    public const string AuthenticationScheme = "MockBearer";
}

public sealed class MockBearerOptions : AuthenticationSchemeOptions;

public sealed class MockBearerHandler(
    IOptionsMonitor<MockBearerOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<MockBearerOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var token = Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(token) && Request.Path.StartsWithSegments("/hubs"))
        {
            token = Request.Query["access_token"].ToString();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing bearer token."));
        }

        var raw = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? token["Bearer ".Length..]
            : token;

        var username = raw.StartsWith("mock-token:", StringComparison.OrdinalIgnoreCase)
            ? raw["mock-token:".Length..]
            : "developer";

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, username),
            new Claim(ClaimTypes.Name, username),
            new Claim("auth_mode", "mock")
        ], Scheme.Name));

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
