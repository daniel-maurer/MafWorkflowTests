namespace MafWorkflow.Shared;

public sealed record MafStartWorkflowCommand(
    string SessionId,
    string WorkflowId,
    string TicketId,
    string? InitialMessage,
    string WorkflowName,
    string Version,
    string InputSchema,
    string? CustomerId = null,
    string? CustomerData = null);

public sealed record MafUserMessageCommand(string SessionId, string Text);
public sealed record MafHumanMessageCommand(string SessionId, string Text);
public sealed record MafRunScenarioCommand(string SessionId, string ScenarioId);
public sealed record MafSessionCommand(
    string SessionId,
    string? CustomerId = null,
    string? CustomerData = null);
