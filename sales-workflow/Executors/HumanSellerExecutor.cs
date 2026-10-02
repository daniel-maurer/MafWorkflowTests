using Microsoft.Agents.AI.Workflows;
using SalesWorkflow.Models;

namespace SalesWorkflow.Executors;

internal sealed class HumanSellerExecutor : Executor<CatalogResult, SalesResolutionResult>
{
    private readonly IUserInteractor _userInteractor;
    private static readonly HashSet<string> EndConversationCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "[COMPLETED]", "COMPLETED", "[FINALIZAR]", "FINALIZAR", "[FIM]", "FIM"
    };

    public HumanSellerExecutor(IUserInteractor userInteractor) : base("HumanSellerExecutor")
    {
        _userInteractor = userInteractor;
    }

    public override async ValueTask<SalesResolutionResult> HandleAsync(
        CatalogResult catalogResult,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        var reason = string.IsNullOrWhiteSpace(catalogResult.OriginalIntent)
            ? "Negociação ou caso especial"
            : $"Intenção: {catalogResult.OriginalIntent}";

        Logger.LogInfo($"[HumanSellerExecutor] Iniciando handoff para vendedor humano ({reason}).");

        await _userInteractor.SendSystemMessageAsync(
            $"Transição de atendimento: caso requer negociação ou vendedor especializado ({reason}).",
            systemStyle: "escalate",
            audience: MessageAudience.Both,
            cancellationToken: cancellationToken);

        await _userInteractor.SendSystemMessageAsync(
            "Vendedor comercial Carlos Eduardo assumiu o atendimento.",
            systemStyle: "handoff",
            audience: MessageAudience.Both,
            cancellationToken: cancellationToken);

        await _userInteractor.PublishSplitModeAsync(true, cancellationToken);
        await _userInteractor.PublishAgentStateAsync("human-seller", "active", "Running", cancellationToken);
        await _userInteractor.PublishContextAsync(
            "human-chat",
            "Atendimento com Vendedor",
            "Carlos Eduardo está negociando com o cliente.",
            "human-seller",
            true,
            cancellationToken);

        await _userInteractor.PublishTraceAsync(
            $"Vendedor humano Carlos Eduardo ingressou na conversa ({reason}).",
            "info",
            cancellationToken);

        bool resolved = false;
        bool ended = false;
        string lastMessage = string.Empty;

        while (!ended)
        {
            string message = await _userInteractor.GetUserResponseAsync(
                string.Empty,
                "human-seller",
                audience: MessageAudience.Both,
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

            lastMessage = message;
        }

        if (resolved)
        {
            await _userInteractor.PublishTraceAsync("Negociação concluída com sucesso pelo vendedor.", "success", cancellationToken);
            await _userInteractor.SendSystemMessageAsync(
                "Atendimento comercial finalizado com sucesso.",
                systemStyle: "resolved",
                audience: MessageAudience.Both,
                cancellationToken: cancellationToken);
        }
        else
        {
            await _userInteractor.PublishTraceAsync("Atendimento do vendedor encerrado.", "warning", cancellationToken);
        }

        await _userInteractor.PublishAgentStateAsync("human-seller", "done", "Done", cancellationToken);
        await _userInteractor.PublishSplitModeAsync(false, cancellationToken);

        var resolution = new SalesResolutionResult
        {
            IsResolved = resolved,
            RequiresHuman = false,
            ResolutionType = "human_handoff",
            MessageForUser = resolved
                ? (string.IsNullOrWhiteSpace(lastMessage) ? "Negociação concluída com sucesso pelo vendedor." : lastMessage)
                : "Atendimento comercial encerrado.",
            ActionsExecuted = ["HumanSellerNegotiation"],
            EscalationReason = reason
        };

        await context.YieldOutputAsync(resolution, cancellationToken);
        return resolution;
    }
}
