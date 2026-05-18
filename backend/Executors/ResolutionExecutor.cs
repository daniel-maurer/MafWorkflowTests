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


        var history = await context.ReadStateAsync<List<ChatMessage>>(Constants.ConversationHistoryKey, Constants.TriageStateScope) ?? new List<ChatMessage>();

        Logger.LogInfo("Starting resolution process...");
        Logger.LogDebug($"Problem - IsKnown: {frequentProblemResult.IsKnown}");
        Logger.LogDebug($"Problem Details: {frequentProblemResult.MessageForUser}");
        await _userInteractor.PublishAgentStateAsync("res", "active", "Running", cancellationToken);
        await _userInteractor.PublishContextAsync(
            "resolving",
            "Resolving issue",
            "Attempting automated resolution.",
            "resolution",
            false,
            cancellationToken);
        await _userInteractor.PublishTraceAsync("Starting resolution attempt", TraceConstants.IconWrench, TraceConstants.ColorPrimary, cancellationToken);
        await _userInteractor.SetAgentTypingAsync("Resolution Agent executing", true, cancellationToken);
        
        var actionsExecuted = new List<string>();

        // If problem is not known, escalate to human
        if (!frequentProblemResult.IsKnown)
        {
            await _userInteractor.SetAgentTypingAsync("Resolution Agent executing", false, cancellationToken);
            await _userInteractor.PublishTraceAsync("Problem not recognized, escalating to human support", TraceConstants.IconSiren, TraceConstants.ColorWarning, cancellationToken);
            await _userInteractor.PublishAgentStateAsync("res", "done", "Done", cancellationToken);
            await _userInteractor.PublishAgentStateAsync("human-support", "active", "Waiting", cancellationToken);
            await _userInteractor.PublishContextAsync(
                "human-chat",
                "Human handoff",
                "Escalating to human support.",
                "human-support",
                true,
                cancellationToken);
            await _userInteractor.PublishSplitModeAsync(true, cancellationToken);
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
            await _userInteractor.SetAgentTypingAsync("Resolution Agent executing", false, cancellationToken);
            await _userInteractor.PublishTraceAsync("No solution found for known issue, escalating to human support", TraceConstants.IconSiren, TraceConstants.ColorWarning, cancellationToken);
            await _userInteractor.PublishAgentStateAsync("res", "done", "Done", cancellationToken);
            await _userInteractor.PublishAgentStateAsync("human-support", "active", "Waiting", cancellationToken);
            await _userInteractor.PublishContextAsync(
                "human-chat",
                "Human handoff",
                "Escalating to human support.",
                "human-support",
                true,
                cancellationToken);
            await _userInteractor.PublishSplitModeAsync(true, cancellationToken);
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
            history.Add(new ChatMessage(ChatRole.User, userMessage));
            await _userInteractor.SendUserResponseAsync(userMessage, BffWorkflowClient.AgentRegistry["res"], cancellationToken: cancellationToken);


            Logger.OutputAgent($"\n{userMessage}");
            actionsExecuted.AddRange(toolsToCall);
        }
        else
        {
            await _userInteractor.PublishTraceAsync("Executing resolution tools", TraceConstants.IconWrench, TraceConstants.ColorPrimary, cancellationToken);
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

                // Record which tools were meant to be called
                actionsExecuted.AddRange(toolsToCall);

                // Publish the resolution message together with the tool invocations so the chat shows:
                //   Resolution Agent  ✅ Resolution applied.
                //     reset_password(...)  ✓ OK
                //     create_ticket(...)   ✓ OK
                var resolutionTools = actionsExecuted
                    .Where(action => !string.IsNullOrWhiteSpace(action))
                    .Select(action => new AgentToolCall { Name = action, Args = string.Empty, Ok = true })
                    .ToList();
                await _userInteractor.SendUserResponseAsync(
                    userMessage,
                    BffWorkflowClient.AgentRegistry["res"],
                    resolutionTools,
                    cancellationToken: cancellationToken);

                Logger.OutputAgent($"\n{userMessage}");
            }
            catch (OperationCanceledException)
            {
                Logger.LogError("Resolution process was cancelled");
                await _userInteractor.PublishTraceAsync("Resolution cancelled by user", TraceConstants.IconSiren, TraceConstants.ColorError, cancellationToken);
                var cancelledResult = new ResolutionResult
                {
                    IsResolved = false,
                    RequiresHuman = true,
                    MessageForUser = "Resolution process was cancelled. Please try again.",
                    ActionsExecuted = actionsExecuted,
                    EscalationReason = "Process was cancelled by user"
                };
                await context.YieldOutputAsync(cancelledResult, cancellationToken);
                await _userInteractor.SetAgentTypingAsync("Resolution Agent executing", false, cancellationToken);
                return cancelledResult;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Exception occurred: {ex.GetType().Name}");
                Logger.LogError($"Message: {ex.Message}");
                Logger.LogDebug($"Stack Trace: {ex.StackTrace}");
                
                await _userInteractor.PublishTraceAsync("Error during resolution attempt", TraceConstants.IconSiren, TraceConstants.ColorError, cancellationToken);
                var errorResult = new ResolutionResult
                {
                    IsResolved = false,
                    RequiresHuman = true,
                    MessageForUser = $"Ocorreu um erro durante a resolução. Um especialista será contatado para ajudar.",
                    ActionsExecuted = actionsExecuted,
                    EscalationReason = $"Error during resolution: {ex.GetType().Name}"
                };
                await context.YieldOutputAsync(errorResult, cancellationToken);
                await _userInteractor.SetAgentTypingAsync("Resolution Agent executing", false, cancellationToken);
                throw;
            }
        }
        
        await _userInteractor.SetAgentTypingAsync("Resolution Agent executing", false, cancellationToken);
        await _userInteractor.PublishTraceAsync("Awaiting user confirmation on resolution", TraceConstants.IconUserCheck, TraceConstants.ColorPrimary, cancellationToken);
        string userConfirmation = await _userInteractor.GetUserResponseAsync("\n✓ Seu problema foi resolvido? (sim/não)", BffWorkflowClient.AgentRegistry["res"], cancellationToken: cancellationToken);

        // The frontend "Mark as Solved" button arrives as a control token in the same channel.
        // Treat it as an affirmative confirmation so Pattern Record runs and the workflow ends
        // cleanly instead of staying stuck waiting for "sim".
        bool markedResolvedFromFrontend = string.Equals(userConfirmation, WorkflowControlTokens.MarkResolved, StringComparison.Ordinal);
        bool resolved = markedResolvedFromFrontend
            || userConfirmation.Trim().ToLowerInvariant() is "sim" or "s" or "yes" or "y" or "ok" or "obrigado" or "valeu" or "resolvido";

        if (resolved)
        {
            await _userInteractor.PublishTraceAsync("Issue resolved successfully", TraceConstants.IconUserCheck, TraceConstants.ColorSuccess, cancellationToken);
            await _userInteractor.PublishAgentStateAsync("res", "done", "Done", cancellationToken);

            var resolvedOutcome = new ResolutionResult
            {
                IsResolved = true,
                RequiresHuman = false,
                MessageForUser = userMessage,
                ActionsExecuted = actionsExecuted,
                EscalationReason = null
            };
            await context.YieldOutputAsync(resolvedOutcome, cancellationToken);
            Logger.LogInfo("Resolution process completed successfully");
            return resolvedOutcome;
        }

        // Negative confirmation. The customer either said "não" or added extra context
        // ("nao, eu pedi sobre vale transporte"). Either way the automated resolution
        // didn't help — escalate to human support inline so the workflow doesn't get
        // stuck waiting at Pattern Record. The customer's correction is published so the
        // attendant can read the reclassification context.
        await _userInteractor.PublishTraceAsync(
            "Resolution unsuccessful, escalating to human support",
            TraceConstants.IconSiren,
            TraceConstants.ColorWarning,
            cancellationToken);
        await _userInteractor.PublishAgentStateAsync("res", "done", "Done", cancellationToken);
        await _userInteractor.SendSystemMessageAsync(
            $"Customer rejected the automated resolution: \"{userConfirmation}\". Routing back for human triage.",
            systemStyle: "escalate",
            icon: "siren",
            audience: MessageAudience.Attendant,
            cancellationToken: cancellationToken);

        var humanOutcome = await HumanSupportSession.RunAsync(
            _userInteractor,
            handoffReason: "automated resolution rejected",
            cancellationToken);

        await context.YieldOutputAsync(humanOutcome, cancellationToken);
        Logger.LogInfo($"Human support handoff after failed automated resolution - Issue resolved: {humanOutcome.IsResolved}");
        return humanOutcome;
        }
    }