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
    private readonly IUserInteractor _userInteractor;
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
    public HumanSupportExecutor(IUserInteractor userInteractor) : base("HumanSupportExecutor")
    {
        this._userInteractor = userInteractor ?? throw new ArgumentNullException(nameof(userInteractor));
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
        await _userInteractor.PublishTraceAsync("Connecting to human support agent", TraceConstants.IconUserCheck, TraceConstants.ColorPrimary, cancellationToken);
        Logger.OutputSystem("\n" + new string('=', 80));
        Logger.OutputSystem("[ATENDENTE SUPORTE] Agora você controla o atendimento humano via terminal.");
        Logger.OutputSystem("[ATENDENTE SUPORTE] Digite a fala do atendente humano e a fala do usuário em sequência.");
        Logger.OutputSystem("[ATENDENTE SUPORTE] Para encerrar o atendimento humano a qualquer momento, digite [COMPLETED] ou FINALIZAR.");
        Logger.OutputSystem(new string('=', 80));

        string humanAgentResponse = await _userInteractor.GetUserResponseAsync("[ATENDENTE HUMANO] ", cancellationToken);
        if (TryCompleteCommand(humanAgentResponse, out var completionResult))
        {
            await _userInteractor.PublishTraceAsync("Human support interaction completed", TraceConstants.IconUserCheck, TraceConstants.ColorPrimary, cancellationToken);
            await context.YieldOutputAsync(completionResult, cancellationToken);
            return completionResult;
        }
        await _userInteractor.PublishTraceAsync("Human support agent engaged", TraceConstants.IconUserCheck, TraceConstants.ColorPrimary, cancellationToken);
        Logger.OutputAgent($"[ATENDENTE HUMANO] {humanAgentResponse}");
        Logger.LogDebug($"Human agent said: {humanAgentResponse}");

        string userReply = await _userInteractor.GetUserResponseAsync("[USUÁRIO] ", cancellationToken);
        if (TryCompleteCommand(userReply, out completionResult))
        {
            await _userInteractor.PublishTraceAsync("Human support interaction completed", TraceConstants.IconUserCheck, TraceConstants.ColorPrimary, cancellationToken);
            await context.YieldOutputAsync(completionResult, cancellationToken);
            return completionResult;
        }
        Logger.OutputUser($"[USUÁRIO] {userReply}");
        Logger.LogDebug($"User replied: {userReply}");

        // Considera a última resposta do atendente como final
        string finalHumanResponse = humanAgentResponse;

        await _userInteractor.PublishTraceAsync("Awaiting user confirmation on resolution", TraceConstants.IconUserCheck, TraceConstants.ColorPrimary, cancellationToken);
        string confirmation = await _userInteractor.GetUserResponseAsync("[USUÁRIO] (sim/não) ", cancellationToken);
        Logger.OutputUser($"[USUÁRIO] {confirmation}\n");
        Logger.LogDebug($"User confirmation: {confirmation}");

        bool isResolved = confirmation.Trim().ToLowerInvariant() is "ok" or "obrigado" or "tá bom" or "valeu" or "sim" or "s" or "yes" or "ok, obrigado" or "muito obrigado" or "resolvido";

        Logger.OutputSystem(new string('=', 80));
        Logger.OutputSystem("[SISTEMA] Finalizando atendimento com suporte humano");
        Logger.OutputSystem(new string('=', 80) + "\n");
        Logger.LogInfo($"Human support interaction completed - Issue resolved: {isResolved}");
        
        if (isResolved)
        {
            await _userInteractor.PublishTraceAsync("Issue resolved by human support", TraceConstants.IconUserCheck, TraceConstants.ColorSuccess, cancellationToken);
        }
        else
        {
            await _userInteractor.PublishTraceAsync("Human support completed - issue not resolved", TraceConstants.IconSiren, TraceConstants.ColorWarning, cancellationToken);
        }

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
