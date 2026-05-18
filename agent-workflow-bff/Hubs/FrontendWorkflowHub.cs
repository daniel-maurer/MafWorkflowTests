using AgentWorkflow.Bff.Contracts;
using AgentWorkflow.Bff.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;

namespace AgentWorkflow.Bff.Hubs;

public sealed class FrontendWorkflowHub(
    ISessionRegistry sessions,
    IMafCommandPublisher maf,
    ILogger<FrontendWorkflowHub> logger) : Hub
{
    public async Task JoinSession(string sessionId)
    {
        logger.LogInformation("SignalR received JoinSession from ConnectionId={ConnectionId} User={User} SessionId={SessionId}", Context.ConnectionId, Context.User?.Identity?.Name, sessionId);
        var snapshot = sessions.GetSnapshot(sessionId)
            ?? throw new HubException($"SESSION_NOT_FOUND: No session with id '{sessionId}'.");

        await Groups.AddToGroupAsync(Context.ConnectionId, sessionId);

        await Clients.Caller.SendAsync("context", sessionId, new
        {
            snapshot.Status,
            snapshot.ChatTitle,
            snapshot.ChatSubtitle,
            snapshot.ActiveAgentId,
            snapshot.Category,
            snapshot.Confidence,
            snapshot.Intent,
            snapshot.HumanMode,
            snapshot.AssignedHumanAgent,
            snapshot.ResolutionSteps
        }, Context.ConnectionAborted);

        foreach (var agent in snapshot.Agents)
        {
            await Clients.Caller.SendAsync("agent", sessionId, agent, Context.ConnectionAborted);
        }

        foreach (var message in snapshot.Messages)
        {
            await Clients.Caller.SendAsync("message", sessionId, message, Context.ConnectionAborted);
        }

        foreach (var trace in snapshot.Trace)
        {
            await Clients.Caller.SendAsync("trace", sessionId, trace, Context.ConnectionAborted);
        }

        await Clients.Caller.SendAsync("kb", sessionId, snapshot.Kb, Context.ConnectionAborted);
        await Clients.Caller.SendAsync("splitMode", sessionId, snapshot.HumanMode, Context.ConnectionAborted);
        logger.LogInformation("SignalR sent session state for JoinSession to ConnectionId={ConnectionId} User={User} SessionId={SessionId}", Context.ConnectionId, Context.User?.Identity?.Name, sessionId);
    }

    public Task LeaveSession(string sessionId)
    {
        logger.LogInformation("SignalR received LeaveSession from ConnectionId={ConnectionId} User={User} SessionId={SessionId}", Context.ConnectionId, Context.User?.Identity?.Name, sessionId);
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, sessionId);
    }

    public Task SendUserMessage(string sessionId, string text)
    {
        logger.LogInformation("SignalR received SendUserMessage from ConnectionId={ConnectionId} User={User} SessionId={SessionId} Text={Text}", Context.ConnectionId, Context.User?.Identity?.Name, sessionId, text);
        return maf.SendUserMessageAsync(new MafUserMessageCommand(sessionId, text), Context.ConnectionAborted);
    }

    public Task SendHumanMessage(string sessionId, string text)
    {
        logger.LogInformation("SignalR received SendHumanMessage from ConnectionId={ConnectionId} User={User} SessionId={SessionId} Text={Text}", Context.ConnectionId, Context.User?.Identity?.Name, sessionId, text);
        return maf.SendHumanMessageAsync(new MafHumanMessageCommand(sessionId, text), Context.ConnectionAborted);
    }

    public Task RunScenario(string sessionId, string scenarioId)
    {
        logger.LogInformation("SignalR received RunScenario from ConnectionId={ConnectionId} User={User} SessionId={SessionId} ScenarioId={ScenarioId}", Context.ConnectionId, Context.User?.Identity?.Name, sessionId, scenarioId);
        return maf.RunScenarioAsync(new MafRunScenarioCommand(sessionId, scenarioId), Context.ConnectionAborted);
    }

    public Task MarkSolved(string sessionId)
    {
        logger.LogInformation("SignalR received MarkSolved from ConnectionId={ConnectionId} User={User} SessionId={SessionId}", Context.ConnectionId, Context.User?.Identity?.Name, sessionId);
        return maf.MarkSolvedAsync(new MafSessionCommand(sessionId), Context.ConnectionAborted);
    }

    public Task ResetSession(string sessionId)
    {
        logger.LogInformation("SignalR received ResetSession from ConnectionId={ConnectionId} User={User} SessionId={SessionId}", Context.ConnectionId, Context.User?.Identity?.Name, sessionId);
        return maf.ResetWorkflowAsync(new MafSessionCommand(sessionId), Context.ConnectionAborted);
    }
}
