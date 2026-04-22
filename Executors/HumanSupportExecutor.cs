using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;

namespace SupportWorkflow;

/// <summary>
/// Executor responsible for simulating human support interaction.
/// Provides a conversation flow between user, support agent, and human specialist.
/// </summary>
internal sealed class HumanSupportExecutor : Executor<FrequentProblemResult, ResolutionResult>
{
    private readonly ConsoleInteractor _consoleInteractor;
    private static readonly HashSet<string> CompletionCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "[COMPLETED]",
        "COMPLETED",
        "[FINALIZAR]",
        "FINALIZAR",
        "[FIM]",
        "FIM"
    };

    /// <summary>
    /// Initializes a new instance of the HumanSupportExecutor.
    /// </summary>
    /// <param name="consoleInteractor">The console interactor for user communication</param>
    public HumanSupportExecutor(ConsoleInteractor consoleInteractor) : base("HumanSupportExecutor")
    {
        this._consoleInteractor = consoleInteractor ?? throw new ArgumentNullException(nameof(consoleInteractor));
    }

    /// <summary>
    /// Handles the simulated human support interaction for complex or unknown issues.
    /// </summary>
    /// <param name="frequentProblemResult">The result from the frequent problem executor</param>
    /// <param name="context">The workflow context for state management</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A ResolutionResult indicating the outcome of human support</returns>
    public override async ValueTask<ResolutionResult> HandleAsync(FrequentProblemResult frequentProblemResult, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (frequentProblemResult == null)
        {
            throw new ArgumentNullException(nameof(frequentProblemResult), "FrequentProblemResult cannot be null");
        }

        Logger.LogInfo("Starting human support handling for complex/unknown issue");
        Logger.OutputUser("\n" + new string('=', 80));
        Logger.OutputUser("[ATENDENTE SUPORTE] Agora você controla o atendimento humano via terminal.");
        Logger.OutputUser("[ATENDENTE SUPORTE] Digite a fala do atendente humano e a fala do usuário em sequência.");
        Logger.OutputUser("[ATENDENTE SUPORTE] Para encerrar o atendimento humano a qualquer momento, digite [COMPLETED] ou FINALIZAR.");
        Logger.OutputUser(new string('=', 80));

        string humanAgentResponse = _consoleInteractor.GetUserResponse("[ATENDENTE HUMANO] ");
        if (TryCompleteCommand(humanAgentResponse, out var completionResult))
        {
            await context.YieldOutputAsync(completionResult, cancellationToken);
            return completionResult;
        }
        Logger.OutputUser($"[ATENDENTE HUMANO] {humanAgentResponse}");
        Logger.LogDebug($"Human agent said: {humanAgentResponse}");

        string userReply = _consoleInteractor.GetUserResponse("[USUÁRIO] ");
        if (TryCompleteCommand(userReply, out completionResult))
        {
            await context.YieldOutputAsync(completionResult, cancellationToken);
            return completionResult;
        }
        Logger.OutputUser($"[USUÁRIO] {userReply}");
        Logger.LogDebug($"User replied: {userReply}");

        // Considera a última resposta do atendente como final
        string finalHumanResponse = humanAgentResponse;

        string confirmation = _consoleInteractor.GetUserResponse("[USUÁRIO] (sim/não) ");
        Logger.OutputUser($"[USUÁRIO] {confirmation}\n");
        Logger.LogDebug($"User confirmation: {confirmation}");

        bool isResolved = confirmation.Trim().ToLowerInvariant() is "ok" or "obrigado" or "tá bom" or "valeu" or "sim" or "s" or "yes" or "ok, obrigado" or "muito obrigado" or "resolvido";

        Logger.OutputUser(new string('=', 80));
        Logger.OutputUser("[SISTEMA] Finalizando atendimento com suporte humano");
        Logger.OutputUser(new string('=', 80) + "\n");
        Logger.LogInfo($"Human support interaction completed - Issue resolved: {isResolved}");

        // Pattern recording is handled later by the PatternRecordExecutor.

        var resolutionResult = new ResolutionResult
        {
            IsResolved = isResolved,
            RequiresHuman = false, // Already handled by human
            MessageForUser = $"Atendimento humano concluído. Problema resolvido: {isResolved}. Última resposta do atendente: {finalHumanResponse}",
            ActionsExecuted = new List<string> { "HumanSupport" },
            EscalationReason = "Problema complexo ou desconhecido - resolvido por especialista humano"
        };

        await context.YieldOutputAsync(resolutionResult, cancellationToken);
        return resolutionResult;
    }

    private static bool TryCompleteCommand(string input, out ResolutionResult completionResult)
    {
        if (input is null)
        {
            completionResult = default!;
            return false;
        }

        if (!CompletionCommands.Contains(input.Trim()))
        {
            completionResult = default!;
            return false;
        }

        completionResult = new ResolutionResult
        {
            IsResolved = false,
            RequiresHuman = false,
            MessageForUser = "Atendimento humano encerrado pelo comando de finalização.",
            ActionsExecuted = new List<string> { "HumanSupport" },
            EscalationReason = "Atendimento humano terminado por comando de encerramento"
        };
        return true;
    }

    private static bool IsCompletionCommand(string input)
    {
        return input is not null && CompletionCommands.Contains(input.Trim());
    }
}
