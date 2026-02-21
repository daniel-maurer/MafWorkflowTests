using System.Text.Json;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

namespace SupportWorkflow;

/// <summary>
/// Executor responsible for attempting automatic resolution of support issues.
/// Coordinates with available tools and escalates complex issues to human support.
/// </summary>
internal sealed class ResolutionExecutor : Executor<FrequentProblemResult, ResolutionResult>
{
    private readonly AIAgent _resolutionAgent;
    private readonly ConsoleInteractor _consoleInteractor;

    /// <summary>
    /// Initializes a new instance of the ResolutionExecutor.
    /// </summary>
    /// <param name="resolutionAgent">The agent for attempting issue resolution</param>
    /// <param name="consoleInteractor">The console interactor for user communication</param>
    public ResolutionExecutor(AIAgent resolutionAgent, ConsoleInteractor consoleInteractor) : base("ResolutionExecutor")
    {
        this._resolutionAgent = resolutionAgent ?? throw new ArgumentNullException(nameof(resolutionAgent));
        this._consoleInteractor = consoleInteractor ?? throw new ArgumentNullException(nameof(consoleInteractor));
    }

    /// <summary>
    /// Handles the resolution attempt for a support issue using available tools.
    /// </summary>
    /// <param name="frequentProblemResult">The result from the frequent problem executor containing the known issue details</param>
    /// <param name="context">The workflow context for state management</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A ResolutionResult indicating if resolution was successful</returns>
    public override async ValueTask<ResolutionResult> HandleAsync(FrequentProblemResult frequentProblemResult, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (frequentProblemResult == null)
        {
            throw new ArgumentNullException(nameof(frequentProblemResult), "FrequentProblemResult cannot be null");
        }

        Console.WriteLine("\n[RESOLUTION EXECUTOR] Starting resolution process...");
        Console.WriteLine($"[RESOLUTION EXECUTOR] Problem - IsKnown: {frequentProblemResult.IsKnown}, IsComplex: {frequentProblemResult.IsComplex}");
        Console.WriteLine($"[RESOLUTION EXECUTOR] Problem Details: {frequentProblemResult.MessageForUser}");
        
        var actionsExecuted = new List<string>();

        // If problem is complex or not known, escalate to human
        if (frequentProblemResult.IsComplex || !frequentProblemResult.IsKnown)
        {
            var escalationResult = new ResolutionResult
            {
                IsResolved = false,
                RequiresHuman = true,
                MessageForUser = "This issue requires human support. A specialist will contact you shortly.",
                EscalationReason = frequentProblemResult.IsComplex ? "Problem is too complex for automation" : "Problem is not recognized in our knowledge base",
                ActionsExecuted = actionsExecuted
            };
            
            await context.YieldOutputAsync(escalationResult, cancellationToken);
            return escalationResult;
        }

        // Problem is known and not complex, attempt resolution
        var toolsToCall = frequentProblemResult.RequiredTools ?? new List<string>();
        Console.WriteLine($"\n[RESOLUTION EXECUTOR] Required tools: {string.Join(", ", toolsToCall)}");
        
        var agentInput = $@"Resolva o seguinte problema:
Problema: {frequentProblemResult.MatchedIssue?.Problem}
Solução: {frequentProblemResult.MatchedIssue?.Solution}
Ferramentas disponíveis: {string.Join(", ", toolsToCall)}
Detalhes do cliente: {frequentProblemResult.MessageForUser}

Use as ferramentas necessárias para resolver o problema:
- UnlockAccount: Desbloquear a conta do usuário
- SendEmail: Enviar email de reset de senha

Explique o que fez para resolver o problema.";

        try
        {
            Console.WriteLine("\n[RESOLUTION EXECUTOR] Calling ResolutionAgent...");
            var response = await this._resolutionAgent.RunAsync(agentInput, cancellationToken: cancellationToken);
            
            Console.WriteLine("\n[RESOLUTION EXECUTOR] Agent Response Received:");
            Console.WriteLine($"{response.Text}");
            
            // Record which tools were meant to be called
            actionsExecuted.AddRange(toolsToCall);
            
            // Ask user for confirmation
            string userConfirmation = _consoleInteractor.GetUserResponse("\n✓ Was your issue resolved? (yes/no)");
            bool resolved = userConfirmation.ToLower() is "yes" or "s" or "y";
            
            var resolutionOutcome = new ResolutionResult
            {
                IsResolved = resolved,
                RequiresHuman = !resolved,
                MessageForUser = response.Text,
                ActionsExecuted = actionsExecuted,
                EscalationReason = !resolved ? "User reported issue not resolved after automated resolution attempt" : null
            };
            
            await context.YieldOutputAsync(resolutionOutcome, cancellationToken);
            Console.WriteLine("\n[RESOLUTION EXECUTOR] Resolution process completed successfully.");
            return resolutionOutcome;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n[RESOLUTION EXECUTOR ERROR] Resolution process was cancelled.");
            var cancelledResult = new ResolutionResult
            {
                IsResolved = false,
                RequiresHuman = true,
                MessageForUser = "Resolution process was cancelled. Please try again.",
                ActionsExecuted = actionsExecuted,
                EscalationReason = "Process was cancelled by user"
            };
            await context.YieldOutputAsync(cancelledResult, cancellationToken);
            return cancelledResult;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[RESOLUTION EXECUTOR ERROR] Exception occurred: {ex.GetType().Name}");
            Console.WriteLine($"[RESOLUTION EXECUTOR ERROR] Message: {ex.Message}");
            Console.WriteLine($"[RESOLUTION EXECUTOR ERROR] Stack Trace: {ex.StackTrace}");
            
            var errorResult = new ResolutionResult
            {
                IsResolved = false,
                RequiresHuman = true,
                MessageForUser = $"An error occurred during resolution: {ex.Message}",
                ActionsExecuted = actionsExecuted,
                EscalationReason = $"Error during resolution: {ex.GetType().Name}"
            };
            await context.YieldOutputAsync(errorResult, cancellationToken);
            throw;
        }
    }
}