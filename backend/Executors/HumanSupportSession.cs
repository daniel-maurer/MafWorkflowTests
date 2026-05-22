using Microsoft.Agents.AI.Workflows;

namespace SupportWorkflow;

/// <summary>
/// Reusable human-support exchange. Runs the split-pane conversation loop and returns
/// a ResolutionResult once the customer marks the issue as solved or the attendant ends
/// the conversation. Used both by <see cref="HumanSupportExecutor"/> (no-KB-match path)
/// and by <see cref="ResolutionExecutor"/> when an automated resolution is rejected by
/// the customer ("não, eu pedi sobre vale transporte"). Centralising the loop avoids
/// drift between the two entry points.
/// </summary>
internal static class HumanSupportSession
{
    private static readonly HashSet<string> EndConversationCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "[COMPLETED]",
        "COMPLETED",
        "[FINALIZAR]",
        "FINALIZAR",
        "[FIM]",
        "FIM"
    };

    public static async Task<ResolutionResult> RunAsync(
        IUserInteractor userInteractor,
        string handoffReason,
        CancellationToken cancellationToken)
    {
        await userInteractor.SendSystemMessageAsync(
            "Freq. Problem Agent → no KB match. Routing to human agent queue...",
            systemStyle: "escalate",
            audience: MessageAudience.Both,
            cancellationToken: cancellationToken);

        await userInteractor.SendSystemMessageAsync(
            "Human agent Daniel M. assigned. Joining the conversation now.",
            systemStyle: "handoff",
            audience: MessageAudience.Both,
            cancellationToken: cancellationToken);

        await userInteractor.PublishSplitModeAsync(true, cancellationToken);
        await userInteractor.PublishAgentStateAsync("human-support", "active", "Running", cancellationToken);
        await userInteractor.PublishContextAsync(
            "human-chat",
            "Human handoff",
            "Daniel M. is talking with the customer.",
            "human-support",
            true,
            cancellationToken);
        await userInteractor.PublishTraceAsync(
            $"Human agent Daniel M. joined the conversation ({handoffReason})",
            "info",
            cancellationToken);

        bool resolved = false;
        bool ended = false;
        string lastHumanMessage = string.Empty;

        while (!ended)
        {
            string message = await userInteractor.GetUserResponseAsync(
                string.Empty,
                "human-support",
                cancellationToken: cancellationToken);

            if (string.Equals(message, WorkflowControlTokens.MarkResolved, StringComparison.Ordinal))
            {
                resolved = true;
                ended = true;
                break;
            }

            if (string.Equals(message, WorkflowControlTokens.Cancel, StringComparison.Ordinal))
            {
                ended = true;
                break;
            }

            if (EndConversationCommands.Contains(message.Trim()))
            {
                ended = true;
                break;
            }

            lastHumanMessage = message;
        }

        if (resolved)
        {
            await userInteractor.PublishTraceAsync(
                "Issue resolved by human support",
                "success",
                cancellationToken);
            await userInteractor.SendSystemMessageAsync(
                "Issue marked as resolved by the customer.",
                systemStyle: "resolved",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);
        }
        else
        {
            await userInteractor.PublishTraceAsync(
                "Human support ended without explicit resolution",
                "warning",
                cancellationToken);
        }

        await userInteractor.PublishAgentStateAsync("human-support", "done", "Done", cancellationToken);
        await userInteractor.PublishSplitModeAsync(false, cancellationToken);

        return new ResolutionResult
        {
            IsResolved = resolved,
            RequiresHuman = false,
            MessageForUser = resolved
                ? lastHumanMessage
                : "Atendimento humano concluído sem confirmação de resolução.",
            ActionsExecuted = new List<string> { "HumanSupport" },
            EscalationReason = resolved
                ? "Resolved by human attendant"
                : "Human attendant ended the conversation without explicit resolution"
        };
    }
}
