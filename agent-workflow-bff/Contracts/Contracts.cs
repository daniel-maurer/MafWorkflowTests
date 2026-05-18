namespace AgentWorkflow.Bff.Contracts;

public sealed record WorkflowDefinitionDto(
    string Id,
    string Title,
    string? Subtitle,
    string Description,
    string Icon,
    string ColorTheme,
    IReadOnlyList<AgentDefinitionDto> Agents,
    IReadOnlyList<ScenarioDefinitionDto> Scenarios,
    WorkflowCapabilitiesDto Capabilities,
    MafWorkflowBindingDto Maf,
    IReadOnlyList<KbItemDto> FixedKnowledgeBase);

public sealed record MafWorkflowBindingDto(
    string WorkflowName,
    string Version,
    string InputSchema);

public sealed record AgentDefinitionDto(
    string Id,
    string Icon,
    string Title,
    string Description,
    string ColorTheme,
    int Order,
    IReadOnlyList<string> Tools);

public sealed record ScenarioDefinitionDto(
    string Id,
    string Title,
    string Label,
    string Description,
    string Message,
    string FlowType);

public sealed record WorkflowCapabilitiesDto(bool HumanHandoff, bool KnowledgeBase, bool Tracing);

public sealed record CreateWorkflowSessionRequest(string WorkflowId, string? InitialMessage);
public sealed record CreateWorkflowSessionResponse(string SessionId, string TicketId);

public sealed record SessionSnapshotDto(
    string SessionId,
    string WorkflowId,
    string TicketId,
    string Status,
    string ChatTitle,
    string ChatSubtitle,
    string? ActiveAgentId,
    string? Category,
    decimal? Confidence,
    string? Intent,
    bool HumanMode,
    HumanAgentDto? AssignedHumanAgent,
    IReadOnlyList<ResolutionStepDto> ResolutionSteps,
    IReadOnlyList<AgentRuntimeStateDto> Agents,
    IReadOnlyList<MessageDto> Messages,
    IReadOnlyList<TraceEventDto> Trace,
    IReadOnlyList<KbItemDto> Kb);

public sealed record HumanAgentDto(string Id, string Name, string Icon);
public sealed record ResolutionStepDto(int Step, string Label, bool Ok);
public sealed record AgentRuntimeStateDto(string Id, string State, string Tag, IReadOnlyList<string> ActiveTools);
public sealed record ToolCallDto(string Name, string Args, bool Ok);

public sealed record MessageDto(
    string Id,
    string Type,
    string Side,
    string SenderType,
    string SenderName,
    string Icon,
    string? BubbleStyle,
    string? SystemStyle,
    string Text,
    IReadOnlyList<ToolCallDto>? Tools,
    DateTimeOffset CreatedAt,
    bool? SplitMirror,
    string? Audience = "both");

public sealed record TraceEventDto(
    string Id,
    DateTimeOffset Time,
    string Icon,
    string Color,
    string Title,
    string? Description,
    string Level);

public sealed record KbItemDto(
    string Id,
    string Title,
    string Category,
    decimal Score,
    string Summary,
    string? ResolutionType,
    IReadOnlyList<string> Tags);

public sealed record TypingEventDto(string Container, string Label, bool On);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, string? NextCursor = null);

public sealed record MafStartWorkflowCommand(
    string SessionId,
    string WorkflowId,
    string TicketId,
    string? InitialMessage,
    string MafWorkflowName,
    string MafWorkflowVersion,
    string InputSchema);

public sealed record MafUserMessageCommand(string SessionId, string Text);
public sealed record MafHumanMessageCommand(string SessionId, string Text);
public sealed record MafRunScenarioCommand(string SessionId, string ScenarioId);
public sealed record MafSessionCommand(string SessionId);

public sealed record MafWorkflowEventEnvelope(
    string SessionId,
    string EventType,
    object Payload,
    DateTimeOffset OccurredAt,
    string? SequenceId = null);

public sealed record ErrorEnvelope(ErrorBody Error)
{
    public static ErrorEnvelope InvalidArgument(string message, object? details = null) =>
        new(new ErrorBody("INVALID_ARGUMENT", message, details));

    public static ErrorEnvelope NotFound(string code, string message, object? details = null) =>
        new(new ErrorBody(code, message, details));
}

public sealed record ErrorBody(string Code, string Message, object? Details = null);
