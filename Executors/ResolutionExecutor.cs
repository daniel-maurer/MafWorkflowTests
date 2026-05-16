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
    private readonly IUserInteractor _userInteractor;

    /// <summary>
    /// Initializes a new instance of the ResolutionExecutor.
    /// </summary>
    /// <param name="resolutionAgent">The agent for attempting issue resolution</param>
    /// <param name="consoleInteractor">The console interactor for user communication</param>
    public ResolutionExecutor(AIAgent resolutionAgent, IUserInteractor userInteractor) : base("ResolutionExecutor")
    {
        this._resolutionAgent = resolutionAgent ?? throw new ArgumentNullException(nameof(resolutionAgent));
        this._userInteractor = userInteractor ?? throw new ArgumentNullException(nameof(userInteractor));
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

        Logger.LogInfo("Starting resolution process...");
        Logger.LogDebug($"Problem - IsKnown: {frequentProblemResult.IsKnown}");
        Logger.LogDebug($"Problem Details: {frequentProblemResult.MessageForUser}");
        
        var actionsExecuted = new List<string>();

        // If problem is not known, escalate to human
        if (!frequentProblemResult.IsKnown)
        {
            var escalationResult = new ResolutionResult
            {
                IsResolved = false,
                RequiresHuman = true,
                MessageForUser = "Este problema requer suporte humano. Um especialista entrará em contato em breve.",
                EscalationReason = "Problem is not recognized in our knowledge base",
                ActionsExecuted = actionsExecuted
            };
            
            await context.YieldOutputAsync(escalationResult, cancellationToken);
            return escalationResult;
        }

        // Problem is known and not complex, attempt resolution
        if (frequentProblemResult.MatchedIssue == null || string.IsNullOrWhiteSpace(frequentProblemResult.MatchedIssue.Solution))
        {
            var escalationResult = new ResolutionResult
            {
                IsResolved = false,
                RequiresHuman = true,
                MessageForUser = "Este problema requer suporte humano. Um especialista entrará em contato em breve.",
                EscalationReason = "Known issue has no resolvable matched issue or solution",
                ActionsExecuted = actionsExecuted
            };
            await context.YieldOutputAsync(escalationResult, cancellationToken);
            return escalationResult;
        }

        var toolsToCall = frequentProblemResult.RequiredTools ?? new List<string>();
        Logger.LogDebug($"Required tools: {string.Join(", ", toolsToCall)}");

        string userMessage;
        if (toolsToCall.Count == 0)
        {
            // If there are no tools required, use the known issue solution directly.
            userMessage = frequentProblemResult.MatchedIssue.Solution?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(userMessage))
            {
                userMessage = "Estamos trabalhando para resolver seu problema. Por favor, aguarde um momento.";
            }

            Logger.OutputAgent($"\n{userMessage}");
            actionsExecuted.AddRange(toolsToCall);
        }
        else
        {
            var agentInput = $@"Resolva o seguinte problema:
Problema: {frequentProblemResult.MatchedIssue?.Problem}
Solução conhecida: {frequentProblemResult.MatchedIssue?.Solution}
Ferramentas disponíveis: {string.Join(", ", toolsToCall)}
Detalhes do cliente: {frequentProblemResult.MessageForUser}

Responda ao usuário usando a solução conhecida de forma direta e clara. Se as ferramentas estiverem disponíveis, mencione apenas as ações executadas.
Use no máximo uma frase direta ao cliente, sem dizer que não é possível resolver.";

            try
            {
                Logger.LogInfo("Calling ResolutionAgent...");
                var response = await this._resolutionAgent.RunAsync(agentInput, cancellationToken: cancellationToken);
                
                Logger.LogInfo("Agent response received");
                Logger.LogDebug($"Agent Response: {response.Text}");
                
                // Parse the JSON response to extract the user message
                userMessage = response.Text;
                try
                {
                    var agentResponse = JsonSerializer.Deserialize<JsonElement>(response.Text);
                    if (agentResponse.TryGetProperty("message_for_user", out var messageElement))
                    {
                        userMessage = messageElement.GetString() ?? response.Text;
                    }
                }
                catch
                {
                    // If parsing fails, use the full response
                    Logger.LogDebug("Failed to parse agent response as JSON, using full response");
                }
                await context.YieldOutputAsync(userMessage, cancellationToken);

                Logger.OutputAgent($"\n{userMessage}");

                // Record which tools were meant to be called
                actionsExecuted.AddRange(toolsToCall);
            }
            catch (OperationCanceledException)
            {
                Logger.LogError("Resolution process was cancelled");
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
                Logger.LogError($"Exception occurred: {ex.GetType().Name}");
                Logger.LogError($"Message: {ex.Message}");
                Logger.LogDebug($"Stack Trace: {ex.StackTrace}");
                
                var errorResult = new ResolutionResult
                {
                    IsResolved = false,
                    RequiresHuman = true,
                    MessageForUser = $"Ocorreu um erro durante a resolução. Um especialista será contatado para ajudar.",
                    ActionsExecuted = actionsExecuted,
                    EscalationReason = $"Error during resolution: {ex.GetType().Name}"
                };
                await context.YieldOutputAsync(errorResult, cancellationToken);
                throw;
            }
        }
        
        // Ask user for confirmation
            string userConfirmation = await _userInteractor.GetUserResponseAsync("\n✓ Seu problema foi resolvido? (sim/não)", cancellationToken);
            bool resolved = userConfirmation.ToLower() is "sim" or "s" or "yes" or "y";
            
            var resolutionOutcome = new ResolutionResult
            {
                IsResolved = resolved,
                RequiresHuman = !resolved,
                MessageForUser = userMessage,
                ActionsExecuted = actionsExecuted,
                EscalationReason = !resolved ? "User reported issue not resolved after automated resolution attempt" : null
            };
            
            await context.YieldOutputAsync(resolutionOutcome, cancellationToken);
            Logger.LogInfo("Resolution process completed successfully");
            return resolutionOutcome;
        }
    }