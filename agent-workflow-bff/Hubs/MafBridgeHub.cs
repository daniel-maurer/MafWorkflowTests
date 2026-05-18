using AgentWorkflow.Bff.Contracts;
using AgentWorkflow.Bff.Services;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AgentWorkflow.Bff.Hubs;

public sealed class MafBridgeHub(
    ISessionRegistry sessions,
    IFrontendEventPublisher frontend,
    ILogger<MafBridgeHub> logger) : Hub
{
    public async Task RegisterWorker(string workerId, IReadOnlyList<string> supportedWorkflowIds)
    {
        logger.LogInformation("SignalR received RegisterWorker from ConnectionId={ConnectionId} WorkerId={WorkerId} SupportedWorkflowIds={WorkflowIds}", Context.ConnectionId, workerId, string.Join(',', supportedWorkflowIds));
        await Groups.AddToGroupAsync(Context.ConnectionId, MafGroups.Workers);

        foreach (var workflowId in supportedWorkflowIds)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, MafGroups.WorkflowWorkers(workflowId));
        }

        await Clients.Caller.SendAsync("registered", workerId, supportedWorkflowIds, Context.ConnectionAborted);
        logger.LogInformation("SignalR sent registered confirmation to ConnectionId={ConnectionId} WorkerId={WorkerId}", Context.ConnectionId, workerId);
    }

    public async Task PublishEvent(MafWorkflowEventEnvelope envelope)
    {
        logger.LogInformation("SignalR received PublishEvent from ConnectionId={ConnectionId} SessionId={SessionId} EventType={EventType}", Context.ConnectionId, envelope.SessionId, envelope.EventType);
        sessions.ApplyMafEvent(envelope);
        await frontend.PublishMafEventAsync(envelope, Context.ConnectionAborted);
        logger.LogInformation("SignalR forwarded PublishEvent to frontend group SessionId={SessionId} EventType={EventType}", envelope.SessionId, envelope.EventType);
    }

    public async Task WorkflowStarted(string sessionId)
    {
        logger.LogInformation("SignalR received WorkflowStarted from ConnectionId={ConnectionId} SessionId={SessionId}", Context.ConnectionId, sessionId);
        await PublishEvent(new MafWorkflowEventEnvelope(
            sessionId,
            "trace",
            new TraceEventDto($"trc_{Guid.NewGuid():N}", DateTimeOffset.UtcNow, "sparkles", "primary", "MAF workflow started", "The MAF application accepted the workflow session.", "info"),
            DateTimeOffset.UtcNow));
    }
}

public static class MafGroups
{
    public const string Workers = "maf-workers";
    public static string WorkflowWorkers(string workflowId) => $"maf-workers:{workflowId}";
}
